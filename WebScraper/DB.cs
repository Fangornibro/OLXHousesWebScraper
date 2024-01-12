using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WebScraper
{
    internal class DB
    {
        MySqlConnection connection;

        public DB()
        {
            connection = new MySqlConnection("server=localhost;port=3306;username=root;password=root");
            using (connection)
            {
                connection.Open();
                var command = new MySqlCommand("CREATE DATABASE IF NOT EXISTS webscraperapartment", connection);
                command.ExecuteNonQuery();
                connection.Close();
                connection = new MySqlConnection("server=localhost;port=3306;username=root;password=root;database=webscraperapartment");
            }
        }

        public void openConnection()
        {
            if (connection.State == System.Data.ConnectionState.Closed)
            {
                try
                {
                    connection.Open();
                }
                catch (MySqlException ex)
                {
                    MessageBox.Show(ex.ToString(), "Error");
                }
            }
        }
        public void closeConnection()
        {
            if (connection.State == System.Data.ConnectionState.Open)
            {
                connection.Close();
            }
        }
        public MySqlConnection getConnection()
        {
            return connection;
        }
    }
}
