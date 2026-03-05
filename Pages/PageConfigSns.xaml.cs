using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Page = System.Windows.Controls.Page;

namespace CustomCodeSystem.Pages
{

    public partial class PageConfigSns : Page
    {

        private List<string> thirdPartyList = new List<string>();

        public PageConfigSns()
        {
            InitializeComponent();
        }

        private void btnThirdPartySelect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx|All files (*.*)|*.*",
                };

                if (openFileDialog.ShowDialog() != true)
                    return;

                var path = openFileDialog.FileName;

                thirdPartyList = ExcelReader.ReadFirstColumnFromFirstSheet(path); // предполагаем List<string>

                textBoxThirdPartyPath.Text = path;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


        }
        private void btnCustomSnsSelect_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel files (*.xls;*.xlsx)|*.xls;*.xlsx|All files (*.*)|*.*",
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            var path = openFileDialog.FileName;

            textBoxCustomSnsPath.Text = path;
        }


        private void btnTestFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var items = ExcelBlockParser.Parse(textBoxCustomSnsPath.Text);

                for (int i = 0; i < items.Count; i++)
                {
                    items[i].OperationalNumber = thirdPartyList[i];
                }

                foreach (var item in items)
                {
                    Console.WriteLine($"Row={item.RowId}, Block={item.Block}, SN={item.SN}, IMEI={item.IMEI}, OP={item.OperationalNumber}");
                }

                var minMax = ExcelBlockParser.GetMinAndMax(items);

                string fileName = $"ALA440_LIST_{minMax.min}_{minMax.max}";


                var dialog = new SaveFileDialog
                {
                    Title = "Save file",
                    Filter = "Text file (*.alalist)|*.alalist|All files (*.*)|*.*",
                    DefaultExt = "alalist",
                    AddExtension = true,
                    FileName = fileName
                };

                if (dialog.ShowDialog() == true)
                {
                    ExcelBlockParser.Save(dialog.FileName, items);
                }

                MessageBox.Show("Issaugota, vyksta nuskaitymas...");

                ExcelBlockParser.ParseTxt(dialog.FileName);

                MessageBox.Show("Nuskaityta");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void btnTestFile3_Click(object sender, RoutedEventArgs e)
        {
            var path = @"C:\AllProjects\ALA\ccs.config";

            string errorMsg;
            string value;

            bool success = ConfigTxtParser.Load(path, out errorMsg);
            if (success)
            {

                success = ConfigTxtParser.TryGetValue("ALA440", "Testavimas", out value, out errorMsg);

                if (!success)
                {
                    MessageBox.Show(errorMsg);
                    return;
                }
                MessageBox.Show(value);
            }
            else
            {
                MessageBox.Show(errorMsg);
                return;
            }

        }

        private void btnTestFile2_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
