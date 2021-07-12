using PeerBackup.Data.DBModel;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace PeerBackup.Data
{
    public static class SQLiteDB
    {
        static string DatabasePath;
        public static string MainDBFile { get; set; }

        static SQLiteDB()
        {
            string processlocation = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            DatabasePath = System.IO.Path.GetDirectoryName(processlocation);
            MainDBFile = System.IO.Path.Combine(DatabasePath, "peerbackup.db");
        }

        private static string MainDBConnString
        {
            get
            {
                return string.Format(string.Format("Data Source={0}", MainDBFile));
            }
        }

        public static WorkerReport FormatNewDatabase()
        {
            try
            {
                SQLiteConnection.CreateFile(MainDBFile);
                using (var s = new SQLiteConnection(MainDBConnString))
                {
                    s.Open();
                    foreach (string cmdstr in SQLiteStrings.Format_MainDB)
                    {
                        RunNonQuery(s, cmdstr, null);
                    }
                }
                // write out default values
                foreach (var kv in SQLiteStrings.SystemTableDefaultValues)
                {
                    WriteValueToSystemTable(kv.Item1, kv.Item2);
                }
                CreateAdminAccount();
                return null;
            }
            catch (Exception ex)
            {
                return new WorkerReport()
                {
                    LogError = string.Format("Unable to format the database. {0}", ex.Message)
                };
            }
        }

        public static WorkerReport InitDatabase()
        {
            try
            {
                using (var s = new SQLiteConnection(MainDBConnString))
                {
                    s.Open();
                    List<KeyValuePair<string, string>> parms = new List<KeyValuePair<string, string>>();
                    parms.Add(new KeyValuePair<string, string>("$Category", "System"));
                    parms.Add(new KeyValuePair<string, string>("$Setting", "Version"));
                    string version = RunValueQuery(s, SQLiteStrings.System_Get_Value, parms);
                    if (string.IsNullOrEmpty(version))
                    {
                        throw new Exception("Incompatible database version.");
                    }
                    // Chance to detect old version and perform an in place database upgrade.
                    switch (version)
                    {
                        case "1.0":
                            return null;
                        default:
                            throw new Exception("Incompatible database version.");
                    }
                }
            }
            catch (Exception ex)
            {
                return new WorkerReport()
                {
                    LogError = string.Format("Unable to start the database. {0}", ex.Message),
                };
            }
        }

        private static void CreateAdminAccount()
        {
            WorkerReport result = CreateNewUser(-1, "admin", "", true, true, "admin");
            if (result != null)
            {
                throw new Exception("Failed to create default admin account.");
            }
        }

        /// <summary>
        /// Returns an error if user does NOT have admin rights. Return NULL if user does have admin rights.
        /// Will always pass and return NULL if activeuserid = -1 (hidden sys account)
        /// </summary>
        /// <param name="actingUsername"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private static WorkerReport VerifyUserIsAdmin(int activeuserid, out User user)
        {
            user = GetUser(activeuserid);
            if (user == null)
            {
                return new WorkerReport()
                {
                    LogError = "Access Denied: no such user."
                };
            }
            if (!user.Enabled)
            {
                return new WorkerReport()
                {
                    LogError = "Access Denied: user is disabled."
                };
            }
            if (!user.IsAdmin)
            {
                return new WorkerReport()
                {
                    LogError = "Access Denied: user is not admin."
                };
            }
            return null;
        }

        /// <summary>
        /// Creates a new user
        /// </summary>
        /// <param name="actinguserID">user account sending this command</param>
        /// <param name="name">new user name</param>
        /// <param name="email">new user email</param>
        /// <param name="isEnabled">is the account allowed to log in</param>
        /// <param name="isAdmin">is the account allowed to use admin function</param>
        /// <param name="password">clear text password to save. This gets hashed with a salt first</param>
        /// <returns></returns>
        public static WorkerReport CreateNewUser(int actinguserID, string name, string email, bool isEnabled, bool isAdmin, string password)
        {
            // verify admin rights for active user
            User ActingUser;
            WorkerReport result = VerifyUserIsAdmin(actinguserID, out ActingUser);
            if (result != null)
            {
                return result;
            }

            // hidden sys account can't be used.
            if (name == "sys")
            {
                return new WorkerReport()
                {
                    LogError = "Error: account already exists."
                };
            }
            // verify specified user doesn't already exist
            User NewUser = GetUser(name);
            if (NewUser != null)
            {
                return new WorkerReport()
                {
                    LogError = "Error: account already exists."
                };
            }
            string salt = CryptoFunctions.GetNewNonce(8);
            string hash = CryptoFunctions.HashPassword(name, salt, password);

            try
            {
                using (var s = new SQLiteConnection(MainDBConnString))
                {
                    s.Open();
                    // ($Name, $Email, $Enabled, $IsAdmin, $Salt, $Password)
                    var parms = new List<KeyValuePair<string, string>>();
                    parms.Add(new KeyValuePair<string, string>("$Name", name));
                    parms.Add(new KeyValuePair<string, string>("$Email", email));
                    parms.Add(new KeyValuePair<string, string>("$Enabled", BoolToStr(isEnabled)));
                    parms.Add(new KeyValuePair<string, string>("$IsAdmin", BoolToStr(isAdmin)));
                    parms.Add(new KeyValuePair<string, string>("$Salt", salt));
                    parms.Add(new KeyValuePair<string, string>("$Password", hash));

                    RunNonQuery(s, SQLiteStrings.User_CreateUser, parms);
                }
                NewAuditItem(actinguserID, "Create Account", string.Format("{0} as {1} {2}", name, isEnabled ? "enabled" : "disabled", isAdmin ? "admin" : "user"));
                // TODO: create audit entry for this new account creation
            }
            catch (Exception ex)
            {
                return new WorkerReport()
                {
                    LogError = ex.Message
                };
            }
            return null;
        }

        /// <summary>
        /// Retrieve a user by the UserID
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public static User GetUser(int userID)
        {
            if (userID == -1)
            {
                return new User()
                {
                    UserID = -1,
                    Name = "sys",
                    Email = "",
                    Enabled = true,
                    IsAdmin = true,
                    Salt = "",
                    Password = "",
                };
            }

            User result = null;
            using (var con = new SQLiteConnection(MainDBConnString))
            {
                con.Open();
                // ($Name, $Email, $Enabled, $IsAdmin, $Salt, $Password)
                var command = con.CreateCommand();
                command.CommandText = SQLiteStrings.User_GetByID;
                command.Parameters.AddWithValue("$UserID", userID.ToString());
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = new User();
                        //UserID, Name, Email, Enabled, IsAdmin, Salt, Password
                        result.UserID = reader.GetInt32(0);
                        result.Name = reader.GetString(1);
                        result.Email = reader.GetString(2);
                        result.Enabled = IntToBool(reader.GetInt32(3));
                        result.IsAdmin = IntToBool(reader.GetInt32(4));
                        result.Salt = reader.GetString(5);
                        result.Password = reader.GetString(6);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Retrieve a user by the username
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static User GetUser(string name)
        {
            User result = null;

            using (var con = new SQLiteConnection(MainDBConnString))
            {
                con.Open();
                // ($Name, $Email, $Enabled, $IsAdmin, $Salt, $Password)
                var command = con.CreateCommand();
                command.CommandText = SQLiteStrings.User_GetByName;
                command.Parameters.AddWithValue("$Name", name);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result = new User();
                        //UserID, Name, Email, Enabled, IsAdmin, Salt, Password
                        result.UserID = reader.GetInt32(0);
                        result.Name = reader.GetString(1);
                        result.Email = reader.GetString(2);
                        result.Enabled = IntToBool(reader.GetInt32(3));
                        result.IsAdmin = IntToBool(reader.GetInt32(4));
                        result.Salt = reader.GetString(5);
                        result.Password = reader.GetString(6);
                    }
                }
            }
            return result;
        }

        private static void WriteValueToSystemTable(string setting, string value)
        {
            using (var s = new SQLiteConnection(MainDBConnString))
            {
                s.Open();
                var parms = new List<KeyValuePair<string, string>>();
                parms.Add(new KeyValuePair<string, string>("$Category", "System"));
                parms.Add(new KeyValuePair<string, string>("$Setting", setting));
                parms.Add(new KeyValuePair<string, string>("$Value", value));
                RunNonQuery(s, SQLiteStrings.System_Set_Value, parms);
            }
        }

        public static WorkerReport NewAuditItem(int userID, string action, string details)
        {
            try
            {
                using (var s = new SQLiteConnection(MainDBConnString))
                {
                    s.Open();
                    // ($UserID, $DateTime, $Action, $Details)
                    var parms = new List<KeyValuePair<string, string>>();
                    parms.Add(new KeyValuePair<string, string>("$UserID", userID.ToString()));
                    parms.Add(new KeyValuePair<string, string>("$DateTime", DateTime.UtcNow.ToString("yyyyMMddHHmmss")));
                    parms.Add(new KeyValuePair<string, string>("$Action", action));
                    parms.Add(new KeyValuePair<string, string>("$Details", details));

                    RunNonQuery(s, SQLiteStrings.Audit_CreateEntry, parms);
                }
                // TODO: create audit entry for this new account creation
            }
            catch (Exception ex)
            {
                return new WorkerReport()
                {
                    LogError = ex.Message
                };
            }
            return null;
        }

        private static string RunValueQuery(SQLiteConnection conn, string query, List<KeyValuePair<string, string>> parms)
        {
            string result = null;
            var command = conn.CreateCommand();
            command.CommandText = query;
            foreach (var kv in parms)
            {
                command.Parameters.AddWithValue(kv.Key, kv.Value);
            }
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    result = reader.GetString(0);
                }
            }
            return result;
        }

        private static void RunNonQuery(SQLiteConnection conn, string query, List<KeyValuePair<string, string>> parms)
        {
            var command = conn.CreateCommand();
            command.CommandText = query;
            if (parms != null)
            {
                foreach (var kv in parms)
                {
                    command.Parameters.AddWithValue(kv.Key, kv.Value);
                }
            }
            command.ExecuteNonQuery();
        }

        private static bool IntToBool(int i)
        {
            if (i == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static string BoolToStr(bool b)
        {
            if (b)
            {
                return "1";
            }
            else
            {
                return "0";
            }
        }

    }
}
