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
                Console.WriteLine("Írd be a plang.jar elérési útvonalát, vagy húzd az ablakra a fájlt! (utána enter)");
                string plangpath = Console.ReadLine() ?? "";
                while (!File.Exists(plangpath) || Path.GetFileName(plangpath) != "plang.jar")
                {
                    Console.WriteLine("Valami hiba van! Ellenőrizd a megadott fájlt! (biztos létezik?, biztos plang.jar a neve?)");
                    plangpath = Console.ReadLine() ?? "";
                }

                Console.WriteLine();
                Console.WriteLine("Mostantól, ha egy programodat akarod ellenőrizni, húzd rá az ablakra, majd üss egy entert!");
                Console.WriteLine("Ha pedig egy feladat tesztjeire vagy kíváncsi, íjr be egy kérdőjelet, majd a feladat kódszámát (pl ?2.26a)!");
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("================================================================");
                    int het;
                    string feladat;
                    string programpath;
                    bool csakListazz = false;

                    try
                    {
                        programpath = Console.ReadLine() ?? throw new Exception();
                        if (programpath.StartsWith("?"))
                        { 
                            csakListazz = true;
                            programpath = programpath[1..] + ".plang";
                        }

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

                    if (tesztek.Count == 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Ehhez a feladathoz nincsenek tesztek...");
                        continue;
                    }


                    if (csakListazz)
                    {
                        foreach (var teszt in tesztek)
                        {
                            Console.WriteLine("A teszt:");
                            foreach (var sor in teszt.Item1.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine("A várt válasz:");
                            foreach (var sor in teszt.Item2.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.WriteLine("================");
                            Console.WriteLine();
                        }
                        continue;
                    }

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
                            Console.WriteLine("========[FUTÁSIDEJŰ HIBA!]========");
                            Console.WriteLine("A teszt:");
                            foreach (var sor in teszt.Item1.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.WriteLine("A program hibája hiba:");
                            foreach (var sor in error.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();

                        }
                        else
                        {
                            List<string> o = new List<string>(output.TrimEnd().Split("\n"));
                            List<string> t = new List<string>(teszt.Item2.TrimEnd().Split("\n"));
                            for (int i = 0; i < o.Count; i++)
                                o[i] = o[i].TrimEnd();
                            for (int i = 0; i < t.Count; i++)
                                t[i] = t[i].TrimEnd();

                            if (Enumerable.SequenceEqual<string>(o, t))
                                Console.WriteLine("=========[SIKERES TESZT!]=========");
                            else
                            {
                                Console.WriteLine("=========[HIBÁS VÁLASZ!]==========");
                                Console.WriteLine("A teszt:");
                                foreach (var sor in teszt.Item1.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
                                Console.WriteLine("A program válasza:");
                                foreach (var sor in output.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
                                Console.WriteLine("A várt válasz:");
                                foreach (var sor in teszt.Item2.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
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
