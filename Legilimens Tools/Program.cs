using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace Legilimens_Starter
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                if (!File.Exists("HLSaveTool/hlsaves.exe"))
                {
                    Console.WriteLine("Couldn't find HLSaveTool! Please download it and put it in HLSaveTool/");
                    return;
                }
                if (!File.Exists("Legilimens.exe"))
                {
                    Console.WriteLine("Couldn't find Legilimens! Please download it and put it in this folder");
                    return;
                }

                string saveFile = SaveFileUtility.GetSavePath();

                string tempDirectory = Directory.GetCurrentDirectory() + "/temp";
                string decompressedSaveFile = Path.Combine(tempDirectory, Path.GetFileNameWithoutExtension(saveFile) + "_decompressed.sav");

                if (!Directory.Exists(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }

                var hlSaveToolProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "HLSaveTool\\hlsaves.exe",
                        Arguments = "-d \"" + saveFile + "\" \"" + decompressedSaveFile + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                hlSaveToolProcess.Start();
                while (!hlSaveToolProcess.StandardOutput.EndOfStream)
                {
                    string line = hlSaveToolProcess.StandardOutput.ReadLine();
                }


                var legilimensProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "Legilimens.exe",
                        Arguments = $"\"{decompressedSaveFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = false
                    }
                };

                legilimensProcess.Start();
                while (!legilimensProcess.StandardOutput.EndOfStream)
                {
                    string line = legilimensProcess.StandardOutput.ReadLine();
                    Console.WriteLine(line);
                }

                Console.WriteLine("Type 1 to run the program again or anything else to quit.");
                string input = Console.ReadLine();
                if (input != "1")
                {
                    return;
                }
            }
        }
    }
}
