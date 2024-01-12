using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GroupDocs.Conversion.FileTypes;
using HtmlAgilityPack;
using MySql.Data.MySqlClient;

namespace WebScraper
{
    public partial class WebScraper : Form
    {
        private static string tableName = "Apartments";
        class Apartment
        {
            public string Link { get; set; }
            public string Directory { get; set; }
            public string Category { get; set; }
            public string Name { get; set; }
            public double PriceGrn { get; set; }
            public double PriceDollar { get; set; }
            public double Area { get; set; }
            public double KitchenArea { get; set; }
            public int NumberOfRooms { get; set; }
            public int Floor { get; set; }
            public int NumberOfStoreys { get; set; }
            public string ObjectType { get; set; }
            public string HouseType { get; set; }
        }
        public WebScraper()
        {
            InitializeComponent();
            BrandCategoryUpdate();
            DataGridUpdate();
        }

        //Current exchange rate
        private double dollarToGrn = 0, euroToGrn = 0, euroToDollar = 0, grnToDollar = 0;
        private void CurrentExchangeRate()
        {
            string s;
            HtmlAgilityPack.HtmlDocument doc;
            HtmlNode linkNode;
            try
            {
                doc = GetDocument("https://minfin.com.ua/ua/currency/");
                linkNode = doc.DocumentNode.SelectSingleNode("(//table[contains(@class, 'table-response mfm-table mfcur-table-lg mfcur-table-lg-currency has-no-tfoot')]//td[contains(@class, 'mfm-text-nowrap')])[1]");
                s = linkNode.InnerText.Replace(" ", "").Replace("\n", "");
                s = s.Remove(5, s.Length - 6);
                dollarToGrn = Convert.ToDouble(s);
            }
            catch
            {
                dollarToGrn = 39.9;
            }
            

            grnToDollar = 1/dollarToGrn;

            try
            {
                doc = GetDocument("https://minfin.com.ua/ua/currency/");
                linkNode = doc.DocumentNode.SelectSingleNode("(//table[contains(@class, 'table-response mfm-table mfcur-table-lg mfcur-table-lg-currency has-no-tfoot')]//td[contains(@class, 'mfm-text-nowrap')])[3]");
                s = linkNode.InnerText.Replace(" ", "").Replace("\n", "");
                s = s.Remove(5, s.Length - 6);
                euroToGrn = Convert.ToDouble(s);
            }
            catch
            {
                euroToGrn = 39.9;
            }

            try
            {
                doc = GetDocument("https://minfin.com.ua/ua/currency/converter/1-eur-to-usd/");
                linkNode = doc.DocumentNode.SelectSingleNode("(//label[contains(@class, 'sc-11fozao-2 heSTnT')])[2]/input");
                euroToDollar = Convert.ToDouble(linkNode.Attributes[1].Value.Replace(".", ","));
            }
            catch
            {
                euroToDollar = 1;
            }
        }

        //Data display
        private void ShowData(string allColumns)
        {
            mainDataGridView.DataSource = null;
            mainDataGridView.Rows.Clear();
            mainDataGridView.Refresh();
            DB db = new DB();
            DataTable dt = new DataTable();
            MySqlDataAdapter da = new MySqlDataAdapter();
            MySqlCommand cmd = new MySqlCommand("SELECT * FROM `" + tableName + "`" + allColumns, db.getConnection());
            da.SelectCommand = cmd;
            try
            {
                da.Fill(dt);
            }
            catch (MySqlException)
            {
                
            }
            mainDataGridView.DataSource = dt;
        }
        private void DataGridUpdate()
        {
            string allColumns = "";
            bool isFirst = true;
            if (categoryCheckedListBox.CheckedItems.Count > 0 || categoryCheckedListBox.CheckedItems.Count > 0)
            {
                foreach (string s in categoryCheckedListBox.CheckedItems)
                {
                    if (isFirst)
                    {
                        allColumns += "WHERE (`" + tableName + "`.Category = '" + Regex.Replace(s, "(\\,[^.]*)$", "") + "'";
                        isFirst = false;
                    }
                    else
                    {
                        allColumns += " OR `" + tableName + "`.Category = '" + Regex.Replace(s, "(\\,[^.]*)$", "") + "'";
                    }
                }
                allColumns += ")";
            }
            ShowData(allColumns);
        }

        private void BrandCategoryUpdate()
        {
            checkBox1.Checked = false;
            categoryCheckedListBox.Items.Clear();

            DB db = new DB();
            DataTable dt1 = new DataTable();
            MySqlDataAdapter da = new MySqlDataAdapter();
            MySqlCommand cmd = new MySqlCommand("SELECT Category, COUNT(*) FROM `" + tableName + "` GROUP BY Category ORDER BY COUNT(*) DESC", db.getConnection());
            da.SelectCommand = cmd;
            try
            {
                da.Fill(dt1);
            }
            catch (MySqlException)
            {
                return;
            }
            string counter;
            for (int i = 0; i < dt1.Rows.Count; i++)
            {
                counter = string.Join(", ", dt1.Rows[i].ItemArray);
                categoryCheckedListBox.Items.Add(counter);
            }
            cmd = new MySqlCommand("SELECT COUNT(*) FROM `" + tableName + "`", db.getConnection());
            da.SelectCommand = cmd;
            dt1.Clear();
            da.Fill(dt1);
            counter = string.Join(", ", dt1.Rows[0].ItemArray);
            checkBox1.Text = "All" + counter;
        }

        //Data scraping
        private int GetNumberOfPages(string Url)
        {
            HtmlAgilityPack.HtmlDocument doc = GetDocument(Url);
            HtmlNode linkNode = doc.DocumentNode.SelectSingleNode("(//section[contains(@class, 'css-j8u5qq')]//li)[last()]/a");
            string link = linkNode.InnerText;
            return Convert.ToInt32(link);
        }

        private List<string> GetLinks(string Url, int numberOfPages)
        {
            HtmlAgilityPack.HtmlDocument doc;
            HtmlNodeCollection linkNodes;
            string link;
            string newUrl;
            List<string> links = new List<string>();
            for (int i = 1; i <= numberOfPages; i++)
            {
                backgroundWorker1.ReportProgress(0);
                newUrl = Url + "?page=" + i + "/";
                doc = GetDocument(newUrl);
                linkNodes = doc.DocumentNode.SelectNodes("//a[contains(@class, 'css-rc5s2u')]");
                if (linkNodes != null)
                {
                    foreach (HtmlNode node in linkNodes)
                    {
                        link = node.Attributes["href"].Value;
                        links.Add(link);
                    }
                }
            }

            return links;
        }

        private int GetLinksCount(string Url, int numberOfPages)
        {
            string newUrl = Url + "?page=" + numberOfPages + "/";
            HtmlAgilityPack.HtmlDocument doc = GetDocument(newUrl);
            HtmlNodeCollection linkNodes = doc.DocumentNode.SelectNodes("//a[contains(@class, 'css-rc5s2u')]");
            int count = ((numberOfPages) * 52) + linkNodes.Count;

            return count;
        }

        private static HtmlAgilityPack.HtmlDocument GetDocument(string Url)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlAgilityPack.HtmlDocument Doc = web.Load(Url);
            return Doc;
        }
        public void startScraping()
        {
            if (!Directory.Exists(textBox1.Text) && textBox1.Text != "")
            {
                MessageBox.Show("The path does not exist.");
                return;
            }
            CurrentExchangeRate();
            scrapButton.Enabled = false;
            button3.Enabled= false;
            label1.Visible = true;
            label1.Text = "";
            progressBar1.Visible = true;
            progressBar1.Value = 0;
            int numberOfPages = 0;
            int count = 0;
            for (int name = 0; name < 3; name++)
            {
                string MainUrl = "";
                switch (name)
                {
                    case 0:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/kvartiry/prodazha-kvartir/kiev/";
                        break;
                    case 1:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/kvartiry/dolgosrochnaya-arenda-kvartir/kiev/";
                        break;
                    case 2:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/komnaty/prodazha-komnat/kiev/";
                        break;
                    case 3:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/komnaty/dolgosrochnaya-arenda-komnat/kiev/";
                        break;
                    case 4:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/posutochno-pochasovo/kiev/";
                        break;
                    case 5:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/doma/arenda-domov/kiev/";
                        break;
                    case 6:
                        MainUrl = "https://www.olx.ua/d/nedvizhimost/doma/prodazha-domov/kiev/";
                        break;
                }
                numberOfPages += GetNumberOfPages(MainUrl);
                count += GetLinksCount(MainUrl, numberOfPages);
            }
            progressBar1.Maximum = count + numberOfPages;
            backgroundWorker1.RunWorkerAsync();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            isContinue = false;
            startScraping();
        }
        private int name;
        private bool isContinue;
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            int rowCount = 0;
            if (isContinue)
            {
                rowCount = mainDataGridView.RowCount;
            }
            else
            {
                CreateTable(tableName);
            }
            for (name = 0; name < 7; name++)
            {
                string mainUrl = "";
                string nameForBigDir = "";
                switch (name)
                {
                    case 0:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/kvartiry/prodazha-kvartir/kiev/";
                        nameForBigDir = "Квартира продажа";
                        break;
                    case 1:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/kvartiry/dolgosrochnaya-arenda-kvartir/kiev/";
                        nameForBigDir = "Квартира аренда долгосрочная";
                        break;
                    case 2:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/komnaty/prodazha-komnat/kiev/";
                        nameForBigDir = "Комната продажа";
                        break;
                    case 3:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/komnaty/dolgosrochnaya-arenda-komnat/kiev/";
                        nameForBigDir = "Комната аренда долгосрочная";
                        break;
                    case 4:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/posutochno-pochasovo/kiev/";
                        nameForBigDir = "Посуточная аренда жилья";
                        break;
                    case 5:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/doma/arenda-domov/kiev/";
                        nameForBigDir = "Дома аренда долгосрочная";
                        break;
                    case 6:
                        mainUrl = "https://www.olx.ua/d/nedvizhimost/doma/prodazha-domov/kiev/";
                        nameForBigDir = "Дома продажа";
                        break;
                }

                int numberOfPages = GetNumberOfPages(mainUrl);
                List<string> links = GetLinks(mainUrl, numberOfPages);
                int startPoint = 0;
                if (isContinue)
                {
                    if (links.Count > rowCount)
                    {
                        startPoint = rowCount;
                        rowCount = 0;
                        backgroundWorker1.ReportProgress(startPoint);
                    }
                    else
                    {
                        rowCount -= links.Count;
                        continue;
                    }
                }
                string path1;
                if (textBox1.Text == "")
                {
                    path1 = Path.Combine(Environment.CurrentDirectory);
                    Directory.CreateDirectory(path1);
                }
                else
                {
                    path1 = textBox1.Text;
                }
                for (int i = startPoint; i < links.Count; i++)
                {
                    Apartment apartment = new Apartment();
                    string link = "https://www.olx.ua/" + links[i];
                    apartment.Link = link;
                    backgroundWorker1.ReportProgress(0);
                    HtmlAgilityPack.HtmlDocument doc = GetDocument(link);


                    //Category
                    apartment.Category = nameForBigDir;


                    //Price
                    HtmlNode priceNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'css-dcwlyx')]/h3");
                    double price;
                    if (priceNode != null)
                    {
                        string priceString = priceNode.InnerText.Replace(" ", "");
                        try
                        {
                            if (priceString.Contains("$"))
                            {
                                price = Convert.ToDouble(priceString.Replace("$", ""));
                                apartment.PriceDollar = Math.Round((Double)price, 1);
                                apartment.PriceGrn = Math.Round((Double)price * dollarToGrn, 1);
                            }
                            else if (priceString.Contains("€"))
                            {
                                price = Convert.ToDouble(priceString.Replace("€", ""));
                                apartment.PriceGrn = Math.Round((Double)price * euroToGrn, 1);
                                apartment.PriceDollar = Math.Round((Double)price * euroToDollar, 1);
                            }
                            else
                            {
                                price = Convert.ToDouble(priceString.Replace("грн.", ""));
                                apartment.PriceGrn = Math.Round((Double)price, 1);
                                apartment.PriceDollar = Math.Round((Double)price * grnToDollar, 1);
                            }
                        }
                        catch { }
                    }


                    //Area
                    HtmlNode areaNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Загальна площа:')]");
                    if (areaNode != null)
                    {
                        string areaText = areaNode.InnerText.Replace("Загальна площа: ", "").Replace(" м²", "").Replace(".", ",");
                        try
                        {
                            double area = Convert.ToDouble(areaText);
                            apartment.Area = area;
                        }
                        catch { }

                    }


                    //Kitchen area
                    HtmlNode kitchenAreaNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Площа кухні:')]");
                    if (kitchenAreaNode != null)
                    {
                        string kitchenAreaText = kitchenAreaNode.InnerText.Replace("Площа кухні: ", "").Replace(" м²", "").Replace(".", ",");
                        try
                        {
                            double kitchenArea = Convert.ToDouble(kitchenAreaText);
                            apartment.KitchenArea = kitchenArea;
                        }
                        catch { }
                    }


                    //NumberOfRooms
                    HtmlNode numberOfRoomsNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Кількість кімнат:')]");
                    int numberOfRooms = 0;
                    if (numberOfRoomsNode != null)
                    {
                        try
                        {
                            numberOfRooms = Convert.ToInt32(numberOfRoomsNode.InnerText.Replace("Кількість кімнат: ", "").Replace("+", "").Replace(" кімнати", "").Replace(" кімната", "").Replace(" кімнат", ""));
                        }
                        catch { }
                    }
                    apartment.NumberOfRooms = numberOfRooms;


                    //Floor
                    HtmlNode floorNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Поверх:')]");
                    int floor = 0;
                    if (floorNode != null)
                    {
                        try
                        {
                            floor = Convert.ToInt32(floorNode.InnerText.Replace("Поверх: ", ""));
                        }
                        catch { }
                    }
                    apartment.Floor = floor;


                    //Number of storeys
                    HtmlNode numberOfStoreysNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Поверховість:')]");
                    int numberOfStoreys = 0;
                    if (numberOfStoreysNode != null)
                    {
                        try
                        {
                            numberOfStoreys = Convert.ToInt32(numberOfStoreysNode.InnerText.Replace("Поверховість: ", ""));
                        }
                        catch { }
                    }
                    apartment.NumberOfStoreys = numberOfStoreys;



                    //Object type
                    HtmlNode objectTypeNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Вид об')]");
                    string objectType = "";
                    if (objectTypeNode != null)
                    {
                        objectType = objectTypeNode.InnerText.Replace("Вид об&#x27;єкта: ", "");
                    }
                    apartment.ObjectType = objectType;


                    //House type
                    HtmlNode houseTypeNode = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'css-1r0si1e')]/p[contains(text(),'Тип будинку:')]");
                    string houseType = "";
                    if (houseTypeNode != null)
                    {
                        houseType = houseTypeNode.InnerText.Replace("Тип будинку: ", "");
                    }
                    apartment.HouseType = houseType;

                    

                    //Images
                    HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'swiper-slide css-1915wzc')]//img");

                    //Name and directory
                    HtmlNode nameForDirNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'css-1soizd2 er34gjf0')]");
                    string nameForDir = "";
                    if (nameForDirNode != null)
                    {
                        nameForDir = (nameForDirNode.InnerText).Replace("\\", "").Replace("/", "").Replace(":", "").Replace("*", "").Replace("?", "").Replace("<", "").Replace(">", "").Replace("|", "").Replace("\"", "");
                        nameForDir = nameForDir.Replace("\n", "");
                    }
                    apartment.Name= nameForDir;
                    string pathForDir;
                    try
                    {
                        pathForDir = Path.Combine(path1, nameForBigDir, nameForDir);
                        apartment.Directory = pathForDir;
                    }
                    catch 
                    { 
                        continue;
                    }
                    


                    InsertProduct(apartment, tableName);



                    //Images downloading
                    if (imageNodes != null)
                    {
                        for (int j = 0; j < imageNodes.Count; j++)
                        {
                            HtmlNode image = imageNodes[j];
                            string fileName = "image" + j.ToString() + ".webp";
                            if (image.Attributes["data-src"] != null)
                            {
                                DownloadImage(image.Attributes["data-src"].Value, fileName, pathForDir);
                            }
                            else if (image.Attributes["src"] != null)
                            {
                                DownloadImage(image.Attributes["src"].Value, fileName, pathForDir);
                            }
                        }
                    }
                }
            }
        }

        private void DownloadImage(string imageUrl, string fileName, string path)
        {
            Directory.CreateDirectory(path);
            string webpPath = Path.Combine(path, fileName);
            using (WebClient client = new WebClient())
            {
                try
                {
                    client.DownloadFile(new Uri(imageUrl), webpPath);
                    using (GroupDocs.Conversion.Converter converter = new GroupDocs.Conversion.Converter(webpPath))
                    {
                        GroupDocs.Conversion.Options.Convert.ImageConvertOptions options = new GroupDocs.Conversion.Options.Convert.ImageConvertOptions
                        {
                            Format = ImageFileType.Jpg
                        };
                        string fileName2 = fileName.Replace(".webp", ".jpg");
                        string jpgPath = Path.Combine(path, fileName2);
                        converter.Convert(jpgPath, options);
                        System.IO.File.Delete(webpPath);
                    }
                }
                catch { }
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage == 0)
            {
                progressBar1.Value += 1;
                label1.Text = "Stage" + (name + 1) + "/7    " + progressBar1.Value + "/" + progressBar1.Maximum;
            }
            else
            {
                progressBar1.Value += e.ProgressPercentage;
                label1.Text = "Stage" + (name + 1) + "/7    " + progressBar1.Value + "/" + progressBar1.Maximum;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar1.Visible = false;
            label1.Visible = false;
            scrapButton.Enabled = true;
            button3.Enabled = true;
        }

        //Table creation
        private void InsertProduct(Apartment apartment, string name)
        {
            DB db = new DB();
            MySqlCommand cmd = new MySqlCommand("INSERT IGNORE INTO " + "`" + name + "`" + " (`Link`, `Directory`, `Category`, `Name`, `Price(GRN)`, `Price($)`, `Area`, `KitchenArea`, `NumberOfRooms`, `Floor`, `NumberOfStoreys`, `ObjectType`, `HouseType`) VALUES(@Li, @Di, @Ca, @Na, @PrG, @PrD, @Ar, @KA, @NOR, @Fl, @NOS, @OT, @HT)", db.getConnection());
            cmd.Parameters.Add("@Li", MySqlDbType.VarChar).Value = apartment.Link;
            cmd.Parameters.Add("@Di", MySqlDbType.VarChar).Value = apartment.Directory;
            cmd.Parameters.Add("@Na", MySqlDbType.VarChar).Value = apartment.Name;
            cmd.Parameters.Add("@Ar", MySqlDbType.Double).Value = apartment.Area;
            cmd.Parameters.Add("@KA", MySqlDbType.Double).Value = apartment.KitchenArea;
            cmd.Parameters.Add("@OT", MySqlDbType.VarChar).Value = apartment.ObjectType;
            cmd.Parameters.Add("@HT", MySqlDbType.VarChar).Value = apartment.HouseType;
            cmd.Parameters.Add("@Fl", MySqlDbType.Int32).Value = apartment.Floor;
            cmd.Parameters.Add("@NOS", MySqlDbType.Int32).Value = apartment.NumberOfStoreys;
            cmd.Parameters.Add("@NOR", MySqlDbType.Int32).Value = apartment.NumberOfRooms;
            cmd.Parameters.Add("@PrG", MySqlDbType.Double).Value = apartment.PriceGrn;
            cmd.Parameters.Add("@PrD", MySqlDbType.Double).Value = apartment.PriceDollar;
            cmd.Parameters.Add("@Ca", MySqlDbType.VarChar).Value = apartment.Category;

            db.openConnection();

            cmd.ExecuteNonQuery();

            db.closeConnection();
        }

        private void CreateTable(string name)
        {
            DropTable(name);
            DB db = new DB();
            MySqlCommand cmd = new MySqlCommand("CREATE TABLE " + "`" + name + "`" + " ( `Link` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL, `Directory` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL, `Category` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL, `Name` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL, `Price(GRN)` DOUBLE UNSIGNED NOT NULL, `Price($)` DOUBLE UNSIGNED NOT NULL, `Area` DOUBLE UNSIGNED NOT NULL, `KitchenArea` DOUBLE UNSIGNED NOT NULL, `NumberOfRooms` INT UNSIGNED NOT NULL, `Floor` INT UNSIGNED NOT NULL, `NumberOfStoreys` INT UNSIGNED NOT NULL, `ObjectType` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL, `HouseType` VARCHAR(256) CHARACTER SET utf8 COLLATE utf8_general_ci NOT NULL) ENGINE = MyISAM CHARSET=utf8 COLLATE utf8_general_ci;", db.getConnection());

            db.openConnection();

            cmd.ExecuteNonQuery();

            db.closeConnection();

            cmd = new MySqlCommand("ALTER TABLE `" + name + "` ADD INDEX(`Link`)", db.getConnection());

            db.openConnection();

            cmd.ExecuteNonQuery();

            db.closeConnection();
        }

        //Other events
        private void categoryCheckedListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (categoryCheckedListBox.Items.Count != categoryCheckedListBox.CheckedItems.Count)
            {
                checkBox1.Checked = false;
            }
            else
            {
                checkBox1.Checked = true;
            }
            DataGridUpdate();
        }
        private void checkBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (checkBox1.Checked)
            {
                for (int i = 0; i < categoryCheckedListBox.Items.Count; i++)
                {
                    categoryCheckedListBox.SetItemChecked(i, true);
                }
            }
            else
            {
                for (int i = 0; i < categoryCheckedListBox.Items.Count; i++)
                {
                    categoryCheckedListBox.SetItemChecked(i, false);
                }
            }
            DataGridUpdate();
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            DataGridUpdate();
            BrandCategoryUpdate();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            isContinue = true;
            startScraping();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();
            textBox1.Text = folderBrowserDialog1.SelectedPath;
        }

        private void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataGridUpdate();
            BrandCategoryUpdate();
        }

        private void DropTable(string name)
        {
            DB db = new DB();
            MySqlCommand cmd = new MySqlCommand("DROP TABLE " + "`" + name + "`", db.getConnection());
            db.openConnection();
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch { }

            db.closeConnection();
        }
    }
}
