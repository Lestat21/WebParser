using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

public class SpecialityAdmission
{
    public string? Name { get; set; }
    public int Plan { get; set; }
    public int Total { get; set; }
    public int WithoutExams { get; set; }
    public int OutOfCompetition { get; set; }
    public Dictionary<string, int> ScoreCounts { get; set; }

    public SpecialityAdmission()
    {
        ScoreCounts = new Dictionary<string, int>
        {
            {"396-400", 0}, {"391-395", 0}, {"386-390", 0}, {"381-385", 0},
            {"376-380", 0}, {"371-375", 0}, {"366-370", 0}, {"361-365", 0},
            {"356-360", 0}, {"351-355", 0}, {"346-350", 0}, {"341-345", 0},
            {"336-340", 0}, {"331-335", 0}, {"326-330", 0}, {"321-325", 0}
        };
    }

    public int SafeParseInt(string input)
    {
        return int.TryParse(input?.Trim(), out int result) ? result : 0;
    }

    public void PrintAnalysis()
    {
        Console.ForegroundColor = Total > Plan ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine($"\nСпециальность: {Name}");
        Console.WriteLine($"План: {Plan}, Факт: {Total}");
        Console.ResetColor();

        if (Total > Plan)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ВНИМАНИЕ: Превышение плана на {Total - Plan} мест!");
            Console.ResetColor();
        }

        Console.WriteLine("Распределение по баллам:");
        foreach (var range in ScoreCounts.Where(x => x.Value > 0))
        {
            Console.WriteLine($"{range.Key}: {range.Value} чел.");
        }
    }
}

class Program
{
    static async System.Threading.Tasks.Task Main(string[] args)
    {
        string url = "https://abitur.bsuir.by/statistics/2025/group/dn_platn.html";

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

            string html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var table = htmlDoc.DocumentNode.SelectSingleNode("//table[@border='1']") ??
                       htmlDoc.DocumentNode.SelectSingleNode("//table");

            if (table == null)
            {
                Console.WriteLine("Таблица не найдена на странице.");
                return;
            }

            List<SpecialityAdmission> admissions = new List<SpecialityAdmission>();
            var rows = table.SelectNodes(".//tr");

            if (rows == null || rows.Count < 2)
            {
                Console.WriteLine("Недостаточно строк в таблице.");
                return;
            }

            // Парсинг данных
            foreach (var row in rows)
            {
                if (row.GetAttributeValue("class", "") == "thead" ||
                    row.GetAttributeValue("class", "") == "econom" ||
                    row.GetAttributeValue("class", "") == "itogo" ||
                    row.SelectSingleNode(".//td[@colspan]") != null)
                    continue;

                var cells = row.SelectNodes(".//td");
                if (cells == null || cells.Count < 21) continue;

                try
                {
                    var spec = new SpecialityAdmission();
                    int cellIndex = cells[0].GetAttributeValue("rowspan", 0) > 0 ? 1 : 0;

                    spec.Name = cells[cellIndex].InnerText.Trim();
                    spec.Plan = spec.SafeParseInt(cells[cellIndex + 1].InnerText);
                    spec.WithoutExams = spec.SafeParseInt(cells[cellIndex + 3].InnerText);
                    spec.OutOfCompetition = spec.SafeParseInt(cells[cellIndex + 4].InnerText);

                    // Заполнение диапазонов баллов
                    for (int i = 0; i < 16; i++)
                    {
                        string key = spec.ScoreCounts.Keys.ElementAt(i);
                        spec.ScoreCounts[key] = spec.SafeParseInt(cells[cellIndex + 5 + i].InnerText);
                    }
                    spec.Total = spec.ScoreCounts.Values.Sum();
                    admissions.Add(spec);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обработке строки: {ex.Message}");
                }
            }

            // ********************** ВСТАВКА АНАЛИТИКИ **********************
            Console.WriteLine("\nАНАЛИЗ ДАННЫХ:");
            Console.WriteLine(new string('=', 50));

            foreach (var spec in admissions)
            {
                spec.PrintAnalysis();
                Console.WriteLine(new string('-', 50));
            }

            PrintGeneralStatistics(admissions);
            // ***************************************************************
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static void PrintGeneralStatistics(List<SpecialityAdmission> admissions)
    {
        Console.WriteLine("\nОБЩАЯ СТАТИСТИКА:");
        Console.WriteLine(new string('=', 50));
        
        int totalPlan = admissions.Sum(x => x.Plan);
        int totalActual = admissions.Sum(x => x.Total);
        
        Console.WriteLine($"Общий план приема: {totalPlan}");
        Console.WriteLine($"Всего подано заявлений: {totalActual}");
        
        // Специальности с превышением плана (красные)
        var overplanned = admissions.Where(x => x.Total > x.Plan).ToList();
        if (overplanned.Any())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nСпециальности с превышением плана:");
            foreach (var spec in overplanned)
            {
                Console.WriteLine($"{spec.Name,-50} | План: {spec.Plan,-4} | Факт: {spec.Total,-4} | +{spec.Total - spec.Plan}");
            }
            Console.ResetColor();
        }

        // Специальности с недобором (желтые)
        var underplanned = admissions.Where(x => x.Total < x.Plan).ToList();
        if (underplanned.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nСпециальности с недобором:");
            foreach (var spec in underplanned)
            {
                Console.WriteLine($"{spec.Name,-50} | План: {spec.Plan,-4} | Факт: {spec.Total,-4} | -{spec.Plan - spec.Total}");
            }
            Console.ResetColor();
        }
        // *******************************************
        var topUnderplanned = admissions
            .Where(x => x.Total < x.Plan)
            .OrderByDescending(x => x.Plan - x.Total)
            .Take(10)
            .ToList();

        if (topUnderplanned.Any())
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nТоп-10! специальностей с наибольшим недобором:");
            Console.WriteLine(new string('-', 60));
            Console.WriteLine("{0,-50} | {1,-10} | {2,-10} | {3,-10}", 
                "Специальность", "План", "Факт", "Недобор");
            Console.WriteLine(new string('-', 60));
            
            foreach (var spec in topUnderplanned)
            {
                int deficit = spec.Plan - spec.Total;
                Console.WriteLine("{0,-50} | {1,-10} | {2,-10} | {3,-10}", 
                    spec.Name, spec.Plan, spec.Total, deficit);
            }
            Console.ResetColor();
        }
        // Дополнительно можно добавить специальности с полным выполнением плана
        var exact = admissions.Where(x => x.Total == x.Plan).ToList();
        if (exact.Any())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\nСпециальности с точным выполнением плана:");
            foreach (var spec in exact)
            {
                Console.WriteLine($"{spec.Name,-50} | План: {spec.Plan,-4} | Факт: {spec.Total,-4}");
            }
            Console.ResetColor();
        }
    }
}