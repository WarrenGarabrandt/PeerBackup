using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PeerBackup.Data.DBModel
{
    public class User
    {
        //UserID INTEGER PRIMARY KEY, Name TEXT, Email TEXT, Enabled INTEGER, IsAdmin INTEGER, Salt TEXT, Password TEXT
        public int UserID { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool Enabled { get; set; }
        public bool IsAdmin { get; set; }
        public string Salt { get; set; }
        public string Password { get; set; }
    }
}
