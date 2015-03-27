using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.IO;
using MySql.Data.MySqlClient;

namespace MailingList
{
    class Program
    {
        static DatabaseConnection db = DatabaseConnection.Instance;
        static char[] SYMBOL_TABLE = ("0123456789abcdefghijklmnopqrstuvwxyz").ToCharArray();

        static void Main(string[] args)
        {
            Message.Verbosity = Verbosity_Level.E_Error | Verbosity_Level.E_Notice;

            /** parsing argument **/
            if (args != null)
            {
                foreach (string arg in args)
                {
                    switch (arg.ToLower())
                    {
                        case "-debug":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Debug;
                            break;
                        case "-warning":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Warning;
                            break;
                        case "-error":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Error;
                            break;
                        case "-notice":
                            Message.Verbosity = Message.Verbosity | Verbosity_Level.E_Notice;
                            break;
                    }
                }
            }

            Message.ShowMessage("Welcome to Mailing List Manager", Verbosity_Level.E_Notice);

            /*Console.WriteLine("Enter hostname :");
            string host = Console.ReadLine();

            Console.WriteLine("Enter username :");
            string username = Console.ReadLine();

            Console.WriteLine("Enter password :");
            string password = Console.ReadLine();

            Console.WriteLine("Enter database name :");
            string db_name = Console.ReadLine();*/

            string host = "localhost", username = "root", password = "", db_name = "email";

            Query query_create_table = new QueryCreateTable("Address")
                                                .Fields("address_id varchar(150) NOT NULL", "local_part varchar(70) NOT NULL",
                                                        "domain_part varchar(25) NOT NULL", "invalid_domain int(1)")
                                                .PrimaryKey("address_id").Build();
            Query query_create_domain_table = new QueryCreateTable("Domain_part").Fields("domain_part varchar(25) NOT NULL", "invalid_domain int(1)").PrimaryKey("domain_part").Build();
            Query query_warning_table = new QueryCreateTable("Warning").Fields("mail varchar(255) NOT NULL").PrimaryKey("mail").Build();

            if (db.Connect(host, username, password, db_name))
            {
                query_create_table.ExecuteQuery(db);
                query_create_domain_table.ExecuteQuery(db);
                query_warning_table.ExecuteQuery(db);
            }
            else
            {
                if (db.Connect(host, username, password))
                {
                    if (db.CreateDatabase(db_name))
                    {
                        if (db.SelectDatabase(db_name))
                        {
                            query_create_table.ExecuteQuery(db);
                            query_create_domain_table.ExecuteQuery(db);
                            query_warning_table.ExecuteQuery(db);
                        }
                        else
                        {
                            Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                            return;
                        }
                    }
                    else
                    {
                        Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                        return;
                    }
                }
                else
                {
                    Message.ShowMessage("Error. Exiting...", Verbosity_Level.E_Error);
                    return;
                }
            }

            Console.WriteLine("\n___________________________________________" +
                                "\n*******************************************" +
                                "\n 1. Import email addresses from files" +
                                "\n 2. Insert an email address" +
                                "\n 3. Delete an email address" +
                                "\n 4. Delete email addresses from file" +
                                "\n 5. Search an email" +
                                "\n 6. Export the database to .txt files" +
                                "\n 7. PING: check which mail servers respond" +
                                "\n 8. Exit" +
                                "\n*******************************************");

            bool exit = false;
            do
            {
                Console.WriteLine("\nEnter 1, 2, 3, 4 , 5 , 6 , 7 or 8 ==> ");
                string choice = Console.ReadLine();
                string email, path, directory_path;

                switch (choice)
                {
                    case "1":
                        Console.WriteLine("Enter path:");
                        path = Console.ReadLine();
                        if (Path.GetExtension(path).Equals(""))
                            Parse_Directory(path);
                        else Parse_File(path);
                        break;

                    case "2":
                        Console.WriteLine("Enter an e-mail: ");
                        email = Console.ReadLine();
                        Insert_Email_Address(email);
                        break;

                    case "3":
                        Console.WriteLine("Enter an e-mail: ");
                        email = Console.ReadLine();
                        Delete_Email_Address(email);
                        break;

                    case "4":
                        Console.WriteLine("Enter a file path: ");
                        path = Console.ReadLine();
                        Delete_Email_From_File(path);
                        break;

                    case "5":
                        Console.WriteLine("Enter an e-mail: ");
                        email = Console.ReadLine();
                        Search_Email_Address(email);
                        break;

                    case "6":
                        Console.WriteLine("Enter a directory path :");
                        directory_path = Console.ReadLine();
                        Export_Alphabetical_Emails(directory_path);
                        Export_All_Emails(directory_path + "/all_emails.txt");
                        break;

                    case "7":
                        Ping_Domains();
                        break;

                    case "8":
                        exit = true;
                        break;

                    default:
                        break;
                }
            }
            while (exit == false);
        }

        public static void Parse_Directory(string directory_path)
        {
            try
            {
                Message.ShowMessage("Parsing Directory {0} ...", Verbosity_Level.E_Notice, directory_path);
                var files = Directory.EnumerateFiles(directory_path);
                foreach (var file in files)
                    Parse_File(file);
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
            finally
            {
                Message.ShowMessage("Done.", Verbosity_Level.E_Notice);
            }
        }

        public static void Parse_File(string file_path)
        {
            try
            {
                char[] delimiters = ("\\,<\">:;[](){} ").ToCharArray();
                string[] tokens = File.ReadAllText(file_path).Split(delimiters);
                foreach (string token in tokens)
                    Insert_Email_Address(token);
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
        }

        //500 alla volta
        public static void Export_Alphabetical_Emails(string directory_path)
        {
            MySqlDataReader reader = null;
            try
            {
                Directory.CreateDirectory(directory_path);
                foreach (char c in SYMBOL_TABLE)
                {
                    Query query = new QuerySelectFromTable("Address").Fields("address_id").WhereFields("local_part").Like("'" + c + "%'").Build();
                    if (query.ExecuteQuery(db, out reader))
                    {
                        DirectoryInfo dir = Directory.CreateDirectory(directory_path + c);
                        using (StreamWriter output = new StreamWriter(dir.FullName + "/" + c + ".txt"))
                        {
                            if (reader.HasRows)
                            {
                                int n_row = 0;
                                while (reader.Read())
                                {
                                    output.WriteLine(reader.GetString(0));
                                    n_row++;
                                }
                            }
                            else
                                Message.ShowMessage("No rows found.", Verbosity_Level.E_Notice);
                        }
                    }
                    if (reader != null)
                        reader.Close();
                }
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
            if (reader != null)
                reader.Close();
        }

        public static void Export_All_Emails(string file_path)
        {
            MySqlDataReader reader = null;
            try
            {
                Query query = new QuerySelectFromTable("Address").Fields("address_id").Build();
                if (query.ExecuteQuery(db, out reader))
                {
                    using (StreamWriter output = new StreamWriter(file_path))
                    {
                        if (reader.HasRows)
                            while (reader.Read())
                                output.WriteLine(reader.GetString(0));
                        else
                            Message.ShowMessage("No rows found.", Verbosity_Level.E_Notice);
                    }
                }
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
            if (reader != null)
                reader.Close();
        }

        public static void Delete_Email_From_File(string file_path)
        {
            try
            {
                char[] delimiters = ("\\,<\">:;[](){} ").ToCharArray();
                string[] tokens = File.ReadAllText(file_path).Split(delimiters);
                foreach (string token in tokens)
                    Delete_Email_Address(token);
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
        }

        public static void Insert_Email_Address(string email_address)
        {
            if (IsValidEmail(email_address))
            {
                string local_address = email_address.Substring(0, email_address.IndexOf("@"));
                string domain_address = email_address.Substring(email_address.IndexOf("@") + 1, email_address.Length - email_address.IndexOf("@") - 1);

                Query query_insert_address = new QueryInsertIntoTable("Address").Fields("address_id", "local_part", "domain_part")
                                                            .Values(email_address, local_address, domain_address).Build();
                Query query_insert_domain_part = new QueryInsertIntoTable("Domain_part").Fields("domain_part").Values(domain_address).Build();
                Query query_warning_domain_part = new QueryInsertIntoTable("Warning").Fields("mail").Values(local_address).Build();

                query_insert_address.ExecuteQuery(db);
                query_insert_domain_part.ExecuteQuery(db);
                if (local_address.Length > 25)
                {
                    query_warning_domain_part.ExecuteQuery(db);
                    Message.ShowMessage("Email {0} insertend into warning table", Verbosity_Level.E_Warning, local_address);
                }
            }
            else Message.ShowMessage("Failed: format is not valid!", Verbosity_Level.E_Warning);
        }

        public static bool Search_Email_Address(string email_address)
        {
            bool success = false;
            try
            {
                MySqlDataReader reader;
                Query query = new QuerySelectFromTable("Address").Count("address_id").WhereFields("address_id").WhereValues(email_address).Build();
                if (query.ExecuteQuery(db, out reader))
                {
                    reader.Read();
                    if (reader.GetInt32(0) > 0)
                    {
                        Message.ShowMessage("Success: email found!", Verbosity_Level.E_Notice);
                        success = true;
                    }
                    else
                    {
                        Message.ShowMessage("Failed: email not found!", Verbosity_Level.E_Warning);
                        success = false;
                    }
                }
                if (reader != null)
                    reader.Close();
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
            return success;
        }

        public static void Delete_Email_Address(string email_address)
        {
            try
            {
                Query query = new QueryDeleteFromTable("Address").Where("address_id", email_address).Build();
                if (IsValidEmail(email_address) && Search_Email_Address(email_address))
                {
                    if (query.ExecuteQuery(db))
                        Message.ShowMessage("Email {0} deleted", Verbosity_Level.E_Notice, email_address);
                }
                else Message.ShowMessage("Email not found", Verbosity_Level.E_Warning);
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
        }

        public static void Ping_Domains()
        {
            try
            {
                Query query = new QuerySelectFromTable("Domain_part").Fields("domain_part").Build();
                MySqlDataReader reader;
                if (query.ExecuteQuery(db, out reader))
                {
                    List<string> domain_names = new List<string>();
                    while (reader.Read())
                        domain_names.Add(reader.GetString(0));
                    reader.Close();

                    foreach (string domain_name in domain_names)
                    {
                        if (Ping(domain_name))
                            Message.ShowMessage("{0} PONG!", Verbosity_Level.E_Notice, domain_name);
                        else
                        {
                            Query query_update_address = new QueryUpdateFromTable("Address").Set("invalid_domain", "1").WhereFields("domain_part").WhereValues(domain_name).Build();
                            Query query_domain_part = new QueryUpdateFromTable("Domain_part").Set("invalid_domain", "1").WhereFields("domain_part").WhereValues(domain_name).Build();
                            if (query_domain_part.ExecuteQuery(db) && query_update_address.ExecuteQuery(db))
                                Message.ShowMessage("{0} is invalid. Flag inserted!", Verbosity_Level.E_Warning, domain_name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
            }
        }

        public static bool Ping(string address)
        {
            Ping pingSender = new Ping();
            PingReply reply = pingSender.Send(address);
            if (reply.Status == IPStatus.Success)
            {
                Message.ShowMessage("Address: {0} RoundTrip time: {1} Time to live: {2} Don't fragment: {3} Buffer size: {4}", Verbosity_Level.E_Notice,
                                        reply.Address.ToString(), reply.RoundtripTime, reply.Options.Ttl, reply.Options.DontFragment, reply.Buffer.Length);
                return true;
            }
            else
            {
                Message.ShowMessage("Ping from address {0} {1}", Verbosity_Level.E_Warning, address, reply.Status);
                return false;
            }
        }

        public static bool IsValidEmail(string string_to_match)
        {
            if (String.IsNullOrEmpty(string_to_match))
                return false;
            try
            {
                return Regex.IsMatch(string_to_match, "^[_a-zA-Z0-9-]+(\\.[_a-zA-Z0-9-]+)*@[a-zA-Z0-9-]+(\\.[a-zA-Z0-9-]+)*(\\.[a-zA-Z]{2,13})$",
                                        RegexOptions.IgnoreCase);
            }
            catch (ArgumentException ex)
            {
                Message.ShowMessage(ex.Message, Verbosity_Level.E_Error);
                return false;
            }
        }
    }
}
