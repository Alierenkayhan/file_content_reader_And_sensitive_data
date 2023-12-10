
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

interface IFileReader
{
    Task<string> ReadFileAsync(string filePath);
}

class PdfFileReader : IFileReader
{
    public async Task<string> ReadFileAsync(string filePath)
    {
        StringBuilder content = new StringBuilder();

        using (PdfReader pdfReader = new PdfReader(filePath))
        {
            using (PdfDocument pdfDocument = new PdfDocument(pdfReader))
            {
                for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                {
                    var listener = new LocationTextExtractionStrategy();
                    PdfCanvasProcessor parser = new PdfCanvasProcessor(listener);
                    parser.ProcessPageContent(pdfDocument.GetPage(page));

                    content.AppendLine(listener.GetResultantText());
                }
            }
        }

        return content.ToString();
    }
}

class TextFileReader : IFileReader
{
    public async Task<string> ReadFileAsync(string filePath)
    {
        using (StreamReader reader = new StreamReader(filePath))
        {
            return await reader.ReadToEndAsync();
        }
    }
}

class Program
{
    static async Task Main()
    {
        SensitiveDataManager sensitiveDataManager = new SensitiveDataManager();

        // Uzantılara göre uygun FileReader seçimi
        IFileReader fileReader = GetFileReader(".pdf");

        sensitiveDataManager.AddSensitiveWord("deneme");
        //sensitiveDataManager.AddSensitiveRule("^[0-9]{4}-[0-9]{2}-[0-9]{2}$");

        string directoryPath = @" ";

        await ReadAndSearchAsync(directoryPath, sensitiveDataManager, fileReader);

        Console.WriteLine("İşlem tamamlandı. Bir tuşa basın...");
        Console.ReadKey();
    }

    static IFileReader GetFileReader(string fileExtension)
    {
        switch (fileExtension.ToLower())
        {
            case ".pdf":
                return new PdfFileReader();
            case ".txt":
                return new TextFileReader();
             default:
                throw new NotSupportedException($"Uzantı desteklenmiyor: {fileExtension}");
        }
    }

    static async Task ReadAndSearchAsync(string directoryPath, SensitiveDataManager sensitiveDataManager, IFileReader fileReader)
    {
        string[] filePaths = Directory.GetFiles(directoryPath);

        foreach (var filePath in filePaths)
        {
            try
            {
                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".pdf" || extension == ".txt")
                {
                    string content = await fileReader.ReadFileAsync(filePath);

                    var results = sensitiveDataManager.Search(filePath, content);
                    double probability = sensitiveDataManager.CheckSensitiveDataProbability(content);

                    if (results.Count > 0 || probability > 0.5) // Örnek bir eşik değeri (0.5) kullanıldı
                    {
                        Console.WriteLine($"Dosya: {filePath}");

                        if (results.Count > 0)
                        {
                            Console.WriteLine("Duyarlı Veriler:");
                            foreach (var result in results)
                            {
                                Console.WriteLine($" - {result.SensitiveWord} (Başlangıç İndeksi: {result.StartIndex})");
                            }
                        }

                        Console.WriteLine($"Olasılık: {probability}");
                        Console.WriteLine();
                    }
                }
                else
                {
                    Console.WriteLine($"Uzantı desteklenmiyor: {extension}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dosya okuma hatası ({filePath}): {ex.Message}");
            }
        }
    }
}

class SensitiveDataManager
{
    private List<string> sensitiveWords;
    private List<Regex> sensitiveRules;

    public SensitiveDataManager()
    {
        sensitiveWords = new List<string>();
        sensitiveRules = new List<Regex>();
    }

    public void AddSensitiveWord(string word)
    {
        sensitiveWords.Add(word.ToLower());
    }

    public void AddSensitiveRule(string rule)
    {
        sensitiveRules.Add(new Regex(rule));
    }

    public List<Result> Search(string filePath, string content)
    {
        List<Result> results = new List<Result>();

        foreach (var word in sensitiveWords)
        {
            int index = content.ToLower().IndexOf(word);
            while (index != -1)
            {
                results.Add(new Result
                {
                    FilePath = filePath,
                    SensitiveWord = word,
                    StartIndex = index
                });
                index = content.ToLower().IndexOf(word, index + 1);
            }
        }

        return results;
    }

    public double CheckSensitiveDataProbability(string text)
    {
        int sensitiveDataCount = 0;

        foreach (var rule in sensitiveRules)
        {
            var matches = rule.Matches(text);
            sensitiveDataCount += matches.Count;
        }

        double probability = (double)sensitiveDataCount / sensitiveRules.Count;
        return probability;
    }
}

class Result
{
    public string FilePath { get; set; }
    public string SensitiveWord { get; set; }
    public int StartIndex { get; set; }
}
