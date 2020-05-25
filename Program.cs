using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NodaTime;
using NodaTime.Text;

namespace FitbitActivtyMerge
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Error: Expected at least 1 file");
                return;
            }

            OffsetDateTimePattern pattern = OffsetDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss.FFFo<m>");

            List<XmlDocument> docs = new List<XmlDocument>();
            List<(OffsetDateTime, XmlDocument)> unorderedDocs = new List<(OffsetDateTime, XmlDocument)>();
            foreach (var file in args)
            {
                XmlDocument doc = new XmlDocument();
                try
                {
                    doc.Load(file);
                }
                catch (XmlException ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }

                var nodeList = doc.GetElementsByTagName("Id");
                var node = nodeList.Item(0);
                ParseResult<OffsetDateTime> time = pattern.Parse(node.InnerText);
                if (!time.Success)
                {
                    Console.WriteLine($"Couldn't parse time: {node.InnerText}");
                    continue;
                }
                unorderedDocs.Add((time.Value, doc));
            }

            unorderedDocs.Sort((d1, d2) => {
                return OffsetDateTime.Comparer.Local.Compare(d1.Item1, d2.Item1);
            });

            Console.WriteLine("Loaded in files");
            Console.Write("New file name: ");
            string newFileName = Console.ReadLine();

            foreach (var doc in unorderedDocs)
            {
                docs.Add(doc.Item2);
            }

            double totalTimeSeconds = 0;
            double distanceMeters = 0;
            int calories = 0;

            foreach (var doc in docs)
            {
                var node = doc.GetElementsByTagName("TotalTimeSeconds").Item(0);
                totalTimeSeconds += double.Parse(node.InnerText);
                distanceMeters += double.Parse(doc.GetElementsByTagName("DistanceMeters").Item(0).InnerText);
                calories += int.Parse(doc.GetElementsByTagName("Calories").Item(0).InnerText);
            }

            docs[0].GetElementsByTagName("TotalTimeSeconds").Item(0).InnerText = totalTimeSeconds.ToString();
            docs[0].GetElementsByTagName("DistanceMeters").Item(0).InnerText = distanceMeters.ToString();
            docs[0].GetElementsByTagName("Calories").Item(0).InnerText = calories.ToString();

            XmlNode firstDocNode = docs[0].GetElementsByTagName("Track").Item(0);

            for (int i = 1; i < docs.Count; i++)
            {
                XmlDocument doc = docs[i];
                var nodeList = doc.GetElementsByTagName("Track").Item(0);
                foreach (XmlNode node in nodeList)
                {
                    XmlNode importedNode = docs[0].ImportNode(node, true);
                    firstDocNode.AppendChild(importedNode);
                }
            }

            docs[0].Save($"{newFileName}.tcx");
            Console.WriteLine("Done");
        }
    }
}
