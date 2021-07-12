using System;
using System.Collections.Generic;

namespace PeerBackup.Data
{
    public static class SQLiteStrings
    {
        public static string[] Format_MainDB = new string[]
        {
            // Contains configuration and version data.
            @"CREATE TABLE System (Category TEXT, Setting TEXT, Value TEXT);",
            // User Table
            @"CREATE TABLE User (UserID INTEGER PRIMARY KEY, Name TEXT, Email TEXT, Enabled INTEGER, IsAdmin INTEGER, Salt TEXT, Password TEXT);",
            // Audit Table
            @"CREATE TABLE Audit (UserID INTEGER, DateTime TEXT, Action TEXT, Details TEXT);",

            //// Basic email header info.
            //@"CREATE TABLE Envelope (EnvelopeID INTEGER PRIMARY KEY, WhenReceived TEXT, Sender TEXT, Recipients TEXT, ChunkCount INTEGER);",
            //// Stores email body in chunks.
            //@"CREATE TABLE MailChunk (EnvelopeID INTEGER NOT NULL, ChunkID INTEGER NOT NULL, Chunk TEXT);",
            //// Queue of items to be transmitted.
            //@"CREATE TABLE SendQueue (SendQueueID INTEGER PRIMARY KEY, EnvelopeID INTEGER NOT NULL, Recipient TEXT, State INTEGER, AttemptCount INTEGER, RetryAfter TEXT);",
            //// Process log. Each attempt to process an email will result in a row being generated with the result of that attempt
            //@"CREATE TABLE SendLog (EnvelopeID INTEGER NOT NULL, Recipient TEXT, WhenSent TEXT, Results TEXT, AttemptCount INTEGER);"
        };

        public static List<Tuple<string, string>> SystemTableDefaultValues = new List<Tuple<string, string>>()
        {
            new Tuple<string, string>("Version", "1.0"),
            new Tuple<string, string>("AdminPipeName", "PeerBackupServiceAdminPipe-3pksxd72mytrsj8osh2lf5d4kubxkia6l4b3rhrpwd6w5imqswbxkmrec"),
        };

        public static string System_Get_Value = @"SELECT Value FROM System WHERE Category = $Category AND Setting = $Setting;";
        public static string System_Set_Value = @"INSERT INTO System(Category, Setting, Value) VALUES ($Category, $Setting, $Value);";

        public static string User_CreateUser = @"INSERT INTO User(Name, Email, Enabled, IsAdmin, Salt, Password) VALUES ($Name, $Email, $Enabled, $IsAdmin, $Salt, $Password);";
        public static string User_GetByID = @"SELECT UserID, Name, Email, Enabled, IsAdmin, Salt, Password FROM User WHERE UserID = $UserID;";
        public static string User_GetByName = @"SELECT UserID, Name, Email, Enabled, IsAdmin, Salt, Password FROM User WHERE Name = $Name;";

        public static string Audit_CreateEntry = @"INSERT INTO Audit(UserID, DateTime, Action, Details) VALUES ($UserID, $DateTime, $Action, $Details);";

    }
}
