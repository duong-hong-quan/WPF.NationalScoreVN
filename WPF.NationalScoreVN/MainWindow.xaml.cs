using DAL.Data;
using DAL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using OfficeOpenXml;
using System.Collections.ObjectModel;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Formats.Asn1.AsnWriter;
using CsvHelper;
using CsvHelper.Configuration;

namespace WPF.NationalScoreVN
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly NationalScoreDBContext NationalScoreDBContext;
        private ObservableCollection<Score> scores;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize your NationalScoreDBContext
            NationalScoreDBContext = new NationalScoreDBContext();

            // Initialize the ObservableCollection for scores
            scores = new ObservableCollection<Score>();
            ScoreDataGrid.ItemsSource = scores;

            // Populate the ComboBox with years from 2017 to 2021
            for (int year = 2017; year <= 2021; year++)
            {
                YearComboBox.Items.Add(year.ToString());
            }
        }

        private void YearComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string selectedYear = YearComboBox.SelectedValue?.ToString();

            if (!string.IsNullOrEmpty(selectedYear))
            {
                // Retrieve scores for the selected year from the database
                scores.Clear(); // Clear the existing ObservableCollection
                var scoresForYear = NationalScoreDBContext.Scores
                    .Where(s => s.Student.SchoolYear.ExamYear == int.Parse(selectedYear))
                    .ToList();

                // Add scores to the ObservableCollection
                foreach (var score in scoresForYear)
                {
                    scores.Add(score);
                }
            }
        }

        private void BrowseButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                FilePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void ImportButtonClick(object sender, RoutedEventArgs e)
        {
            string filePath = FilePathTextBox.Text;
            string selectedYear = YearComboBox.SelectedValue?.ToString();

            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(selectedYear))
            {
                MessageBox.Show("Please select a file and a year before importing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Implement your logic to import data from the file and update the database
                ImportDataFromFile(filePath, selectedYear);

                MessageBox.Show("Import successful.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearDatabaseButtonClick(object sender, RoutedEventArgs e)
        {
            string selectedYear = YearComboBox.SelectedValue?.ToString();

            if (string.IsNullOrEmpty(selectedYear))
            {
                MessageBox.Show("Please select a year before clearing the database.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Implement your logic to clear database scores for the selected year
                ClearDatabaseScores(selectedYear);

                MessageBox.Show("Database cleared successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during database clearing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportDataFromFile(string filePath, string selectedYear)
        {
            if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                // Handle Excel file
                ImportDataFromExcel(filePath, selectedYear);
            }
            else if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                // Handle CSV file
                ImportDataFromCsv(filePath, selectedYear);
            }
            else
            {
                MessageBox.Show("Unsupported file format. Please select a valid Excel (.xlsx) or CSV (.csv) file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportDataFromExcel(string filePath, string selectedYear)
        {
            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];

                for (int row = 2; row <= worksheet.Dimension.Rows; row++)
                {
                    string studentCode = worksheet.Cells[row, 1].Value?.ToString();
                    double grade = Convert.ToDouble(worksheet.Cells[row, 2].Value);

                    if (NationalScoreDBContext.Scores.Any(s => s.Student.StudentCode == studentCode && s.Student.SchoolYear.ExamYear == int.Parse(selectedYear)))
                    {
                        throw new InvalidOperationException($"Score for student {studentCode} in year {selectedYear} already exists in the database.");
                    }

                    var student = NationalScoreDBContext.Students.FirstOrDefault(s => s.StudentCode == studentCode);
                    if (student == null)
                    {
                        student = new DAL.Models.Student { StudentCode = studentCode, SchoolYearId = await GetOrCreateSchoolYearId(selectedYear) };
                        NationalScoreDBContext.Students.Add(student);
                    }

                    string subjectCode = worksheet.Cells[row, 3].Value?.ToString();
                    var subject = NationalScoreDBContext.Subject.FirstOrDefault(s => s.Code == subjectCode);
                    if (subject == null)
                    {
                        throw new InvalidOperationException($"Subject with code {subjectCode} not found in the database.");
                    }

                    var score = new DAL.Models.Score { StudentId = student.Id, SubjectId = subject.Id, Grade = grade };
                    NationalScoreDBContext.Scores.Add(score);
                }

                NationalScoreDBContext.SaveChanges();
            }
        }

        private async void ImportDataFromCsv(string filePath, string selectedYear)
        {
            using (var reader = new StreamReader(filePath, Encoding.Default))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
            {
                using (var NationalScoreDBContext = new NationalScoreDBContext())
                {


                    var records = csv.GetRecords<YourCsvModel>(); // Replace YourCsvModel with the actual CSV model class

                foreach (var record in records)
                {
                    if (NationalScoreDBContext.Scores.Any(s => s.Student.StudentCode == record.SBD && s.Student.SchoolYear.ExamYear == int.Parse(record.Year)))
                    {
                        throw new InvalidOperationException($"Score for student {record.SBD} in year {record.Year} already exists in the database.");
                    }

                    var student = await NationalScoreDBContext.Students.FirstOrDefaultAsync(s => s.StudentCode == record.SBD);
                    if (student == null)
                    {
                        student = new DAL.Models.Student { StudentCode = record.SBD, SchoolYearId = await GetOrCreateSchoolYearId(record.Year.ToString()) };
                       await NationalScoreDBContext.Students.AddAsync(student);
                        await NationalScoreDBContext.SaveChangesAsync();
                    }

                    var subjects = new List<Subject>();
                    var scores = new List<Score>();

                    // Assuming that the properties Toan, Van, Ly, Sinh, NgoaiNgu represent subjects
                    var subjectProperties = typeof(YourCsvModel).GetProperties()
                        .Where(prop => prop.Name != "SBD" && prop.Name != "Year");

                    foreach (var subjectProperty in subjectProperties)
                    {
                        var subjectCode = subjectProperty.Name;
                        var subjectName = subjectCode;

                        var subject = await NationalScoreDBContext.Subject.FirstOrDefaultAsync(s => s.Code == subjectName);
                        if (subject == null)
                        {
                            subject = new DAL.Models.Subject { Code = subjectCode, Name = subjectName };
                            NationalScoreDBContext.Subject.Add(subject);
                            await NationalScoreDBContext.SaveChangesAsync();

                        }

                        var studentId = int.Parse(record.SBD); // Adjust the property name as per your CSV model
                        var existingStudent = await NationalScoreDBContext.Students.SingleOrDefaultAsync(s=> s.StudentCode ==studentId.ToString());

                        if (existingStudent == null)
                        {
                            throw new InvalidOperationException($"Student with Id {studentId} not found in the database.");
                        }

                        // Ensure a safe conversion of the grade
                        if (double.TryParse(subjectProperty.GetValue(record)?.ToString(), out var grade))
                        {
                            var score = new DAL.Models.Score { StudentId = existingStudent.Id, SubjectId = subject.Id, Grade = grade };
                            scores.Add(score);
                        }

                        else
                        {
                            // Log or handle the case where the grade is not a valid double
                            Console.WriteLine($"Invalid grade for {subjectCode}. Skipping.");
                        }
                    }

                    await NationalScoreDBContext.Scores.AddRangeAsync(scores);
                   await NationalScoreDBContext.SaveChangesAsync();


                }
                }
            }
        }

        private async void ClearDatabaseScores(string selectedYear)
        {
            using (var NationalScoreDBContext = new NationalScoreDBContext())
            {
                // Get the SchoolYearId for the selected year
                int schoolYearId = await GetOrCreateSchoolYearId(selectedYear);

                // Retrieve and remove scores for the selected year
                var scoresToRemove = NationalScoreDBContext.Scores.Where(s => s.Student.SchoolYear.ExamYear == int.Parse(selectedYear));
                var studentToRemove = NationalScoreDBContext.Students.Where(s => s.SchoolYear.ExamYear == int.Parse(selectedYear));
                var schoolYear = NationalScoreDBContext.SchoolYears.SingleOrDefault(s => s.ExamYear == int.Parse(selectedYear));
                NationalScoreDBContext.Scores.RemoveRange(scoresToRemove);
                NationalScoreDBContext.Students.RemoveRange(studentToRemove);
                NationalScoreDBContext.SchoolYears.Remove(schoolYear);
                // Save changes to the database
                await NationalScoreDBContext.SaveChangesAsync();
            }
        }

        private async Task<int> GetOrCreateSchoolYearId(string selectedYear)
        {
            using (var NationalScoreDBContext = new NationalScoreDBContext())
            { 
                // Check if the SchoolYear already exists in the database
                var schoolYear = NationalScoreDBContext.SchoolYears.FirstOrDefault(sy => sy.ExamYear == int.Parse(selectedYear));

            if (schoolYear == null)
            {
                // Create the SchoolYear entity and add it to the database
                schoolYear = new SchoolYear { ExamYear = int.Parse(selectedYear), Name = $"School Year {selectedYear}" };
                NationalScoreDBContext.SchoolYears.Add(schoolYear);
              await  NationalScoreDBContext.SaveChangesAsync(); // Save changes to generate the SchoolYearId
            }

            return schoolYear.Id;
            }
        }
    }
}