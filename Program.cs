using System.Net.Http;
using System.IO;
using System.Reflection.Emit;
using System.Diagnostics;
using System;
using System.Text;
using System.Runtime.InteropServices;

namespace ProghaziEllenor
{
    internal class Program
    {
        static string mytempfolder;
        static string mytempfile;

        // https://learn.microsoft.com/en-us/windows/console/setconsolectrlhandler?WT.mc_id=DT-MVP-5003978
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

        // https://learn.microsoft.com/en-us/windows/console/handlerroutine?WT.mc_id=DT-MVP-5003978
        private delegate bool SetConsoleCtrlEventHandler(CtrlType sig);

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        static List<(string, string)> Tesztek(int het, string feladat)
        {
            var ret = new List<(string, string)> ();
            for (int i = 1; true; i++)
            {
            HttpClient hc = new HttpClient();
                var respone = hc.Send(new HttpRequestMessage(HttpMethod.Get, $"https://raw.githubusercontent.com/flugi/bp1_feladatok/master/{het}.{feladat}.{i}.input"));
                if (respone.IsSuccessStatusCode) 
                {
                    string input;
                    string output;
                    input = new StreamReader(respone.Content.ReadAsStream()).ReadToEnd();
                    respone = hc.Send(new HttpRequestMessage(HttpMethod.Get, $"https://raw.githubusercontent.com/flugi/bp1_feladatok/master/{het}.{feladat}.{i}.output"));
                    if (respone.IsSuccessStatusCode)
                        output = new StreamReader(respone.Content.ReadAsStream()).ReadToEnd();
                    else
                    {
                        Console.WriteLine("nem sikerült letölteni a megfelelő teszteket!");
                        return new List<(string, string)>();
                    }
                    if (output.EndsWith("\n"))      // HA \n-re végződik, akkor azt leszedjük!!! elég drasztikus, de szükségesnek tűnik
                        output = output.Substring(0, output.Length - 1);
                    ret.Add((input, output));

                }
                else
                {
                    break;
                }
            }
            return ret;
        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Latin2 magic

            Console.CancelKeyPress += (o, e) => { Stop(); };
            SetConsoleCtrlHandler(Handler, true);


            var temp = Path.GetTempPath();
            var mytemp = Directory.CreateDirectory(Path.Combine(temp, "proghaziellenor"));
            mytempfolder = mytemp.FullName;
            mytempfile = Path.Combine(mytempfolder, "temp.plang");
            try
            {
                Label:
                Console.WriteLine("Írd be a plang.jar elérési útvonalát, vagy húzd az ablakra a fájlt! (utána enter)");
                string plangpath = Console.ReadLine() ?? "";
                if (!File.Exists(plangpath))
                {
                    Console.WriteLine("ellenőrizd a plang.jar fájlt! (biztos létezik?)");
                    goto Label;
                }
                if (Path.GetFileName(plangpath) != "plang.jar")
                {
                    Console.WriteLine("ellenőrizd a plang.jar fájlt! (biztos azt a fájlt adtad meg?)");
                    goto Label;
                }
                Console.WriteLine("Mostantól mindig húzd az ablakra az ellenőrzendő fájlt, majd üss egy enter!");
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("================================================================");
                    int het;
                    string feladat;
                    string programpath;
                    try
                    {
                        programpath = Console.ReadLine();
                        var filename = Path.GetFileName(programpath);
                        var sf = filename.Split(".");
                        if (sf.Length != 3) throw new Exception();
                        het = int.Parse(sf[0]);
                        feladat = sf[1];
                        if (sf[2] != "plang") throw new Exception();
                    }
                    catch
                    {
                        Console.WriteLine("hibás fájlnév!");
                        continue;
                    }
                    var tesztek = Tesztek(het, feladat);

                    foreach (var teszt in tesztek)
                    {
                        string output;
                        string error;

                        var fs = File.Create(mytempfile);
                        var sw = new StreamWriter(fs, new UTF8Encoding(false));
                        try
                        {
                            var sr = new StreamReader(programpath, Encoding.GetEncoding("ISO-8859-2"));
                            string kod = sr.ReadToEnd();
                            sw.Write(kod);
                            sw.Close();
                            sr.Close();

                            ProcessStartInfo startInfo = new ProcessStartInfo();
                            startInfo.CreateNoWindow = false;
                            startInfo.UseShellExecute = false;
                            startInfo.FileName = "java";
                            startInfo.Arguments = $"-jar {plangpath} {mytempfile}";
                            startInfo.RedirectStandardError = true;
                            startInfo.RedirectStandardInput = true;
                            startInfo.RedirectStandardOutput = true;

                            using (Process exeProcess = Process.Start(startInfo))
                            {
                                exeProcess.StandardInput.Write(teszt.Item1.Replace("\r", ""));
                                exeProcess.StandardInput.Close();
                                exeProcess.WaitForExit();
                                

                                output = new StreamReader(exeProcess.StandardOutput.BaseStream, Encoding.GetEncoding("ISO-8859-2")).ReadToEnd();
                                error = new StreamReader(exeProcess.StandardError.BaseStream, Encoding.GetEncoding("ISO-8859-2")).ReadToEnd();
                            }
                        }
                        finally
                        {
                            File.Delete(mytempfile);
                        }

                        if (error != "")
                        {
                            Console.WriteLine("SIKERTELEN FUTÁS:");
                            foreach (var sor in error.Split("\n"))
                                Console.WriteLine("> " + sor);
                        }
                        else
                        {
                            if (output == teszt.Item2)
                                Console.WriteLine("SIKERES TESZT!");
                            else
                            {
                                Console.WriteLine("SIKERTELEN TESZT!");
                                string message = $"A teszt:\n{teszt.Item1}\n\nA program válasza:\n{output}\n\nA várt válasz:\n{teszt.Item2}";
                                foreach (var sor in message.Split("\n"))
                                    Console.WriteLine("> " + sor);
                            }
                        }
                    }
                }
            }
            finally
            {
                Stop();
            }
            
        }

        private static bool Handler(CtrlType signal)
        {
            switch (signal)
            {
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    Stop();
                    Environment.Exit(0);
                    return false;

                default:
                    return false;
            }
        }

        private static void Stop()
        {
            Directory.Delete(mytempfolder, true);
        }
    }
}
