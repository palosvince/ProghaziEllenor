using System.Net.Http;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;


namespace ProghaziEllenor
{
    internal class Program
    {
        static string mytempfolder = "";
        static bool PlangMode;

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
            var ret = new List<(string, string)>();
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
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("nem sikerült letölteni a megfelelő teszteket!");
                        Console.ForegroundColor = ConsoleColor.White;
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

        static (string, string) Run_Plang(string plangpath, string file, string input)
        {
            string output;
            string error;

            string mytempfile = Path.Combine(mytempfolder, "temp.plang");
            var fs = File.Create(mytempfile);
            try
            {
                var sw = new StreamWriter(fs, new UTF8Encoding(false));
                var sr = new StreamReader(file, Encoding.GetEncoding("ISO-8859-2"));
                string kod = sr.ReadToEnd();
                sw.Write(kod);
                sw.Close();
                sr.Close();

                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = false;
                startInfo.UseShellExecute = false;
                startInfo.FileName = "java";
                startInfo.Arguments = $@"-jar ""{plangpath}"" ""{mytempfile}""";
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;
                startInfo.RedirectStandardOutput = true;


                using (Process? exeProcess = Process.Start(startInfo))
                {
                    if (exeProcess == null)
                    {
                        return ("", "Valami rosszul sikerült, biztos, hogy a valódi plang.jart adtad meg?");
                    }
                    exeProcess.StandardInput.Write(input);
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

            return (output, error);
        }

        static (string output, string compError, string runError) Run_Cpp(string gccpath, string file, string input)
        {
            string output;
            string compError;
            string runError;

            // Compile
            ProcessStartInfo compileStart = new ProcessStartInfo();
            compileStart.CreateNoWindow = false;
            compileStart.UseShellExecute = false;
            compileStart.EnvironmentVariables["PATH"] = compileStart.EnvironmentVariables["PATH"] + ";" + Path.GetDirectoryName(gccpath);
            compileStart.FileName = gccpath;
            compileStart.Arguments = $@"""{file}"" -Wall -o""{mytempfolder}/{Path.GetFileNameWithoutExtension(file)}.exe""";
            compileStart.RedirectStandardError = true;
            compileStart.RedirectStandardInput = true;
            compileStart.RedirectStandardOutput = true;

            using (Process compile = Process.Start(compileStart))
            {
                if (compile == null)
                {
                    return ("", "Valami rosszul sikerült, biztos, hogy a valódi plang.jart adtad meg", "");
                }
                compile.WaitForExit();
                compError = compile.StandardError.ReadToEnd();
                if (!string.IsNullOrEmpty(compError))
                    return ("", compError, "");
            }

            //run
            ProcessStartInfo exeStart = new ProcessStartInfo();
            exeStart.CreateNoWindow = false;
            exeStart.UseShellExecute = false;
            exeStart.EnvironmentVariables["PATH"] = compileStart.EnvironmentVariables["PATH"] + ";" + Path.GetDirectoryName(gccpath);
            exeStart.FileName = $@"{mytempfolder}/{Path.GetFileNameWithoutExtension(file)}.exe";
            exeStart.RedirectStandardError = true;
            exeStart.RedirectStandardInput = true;
            exeStart.RedirectStandardOutput = true;

            using (Process exe = Process.Start(exeStart))
            {
                if (exe == null)
                {
                    return ("", "", "Úgy tűnik, mégse sikerült a fordítás");
                }
                exe.StandardInput.Write(input);
                exe.StandardInput.Close();
                exe.WaitForExit();

                runError = exe.StandardError.ReadToEnd();
                output = exe.StandardOutput.ReadToEnd();
            }

            return (output, compError, runError);
        }


        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("ProghaziEllenor v3");
            Console.WriteLine("készítette: Pálos Vince - palos.vince@hallgato.ppke.hu");
            Console.WriteLine("Forráskód: https://github.com/palosvince/ProghaziEllenor");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();


            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Latin2 magic

            Console.CancelKeyPress += (o, e) => { Stop(); };
            SetConsoleCtrlHandler(Handler, true);

            Console.ForegroundColor = ConsoleColor.White;


            var temp = Path.GetTempPath();
            var mytemp = Directory.CreateDirectory(Path.Combine(temp, "proghaziellenor"));
            mytempfolder = mytemp.FullName;
            try
            {
                Console.WriteLine("Szia! Milyen programnyelvet szeretnél ellenőrizni?");
                Console.WriteLine("Plang kód ellenőrzéséhez üss P betűt, ha C++-hoz üss C betűt!");
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    bool? pm = null;
                    do
                    {
                        char s = Console.ReadKey().KeyChar;
                        Console.WriteLine();
                        if (s == 'P' || s == 'p')
                            pm = true;
                        if (s == 'C' || s == 'c')
                            pm = false;
                    } while (pm is null);
                    Console.ForegroundColor = ConsoleColor.White;
                    PlangMode = pm ?? false;
                }


                string plangpath = "";
                string gccpath = "";
                if (PlangMode)
                {
                    Console.WriteLine("Írd be a plang.jar elérési útvonalát, vagy húzd az ablakra a fájlt! (utána enter)");
                    Console.ForegroundColor= ConsoleColor.Yellow;
                    plangpath = (Console.ReadLine() ?? "").Replace(@"""", "");
                    Console.ForegroundColor = ConsoleColor.White;
                    while (!File.Exists(plangpath) || Path.GetFileName(plangpath) != "plang.jar")
                    {
                        Console.WriteLine("Valami hiba van! Ellenőrizd a megadott fájlt! (biztos létezik?, biztos plang.jar a neve?)");
                        plangpath = Console.ReadLine() ?? "";
                    }
                }
                else
                {
                    if (File.Exists("C:\\Program Files\\CodeBlocks\\MinGW\\bin\\g++.exe"))
                        gccpath = "C:\\Program Files\\CodeBlocks\\MinGW\\bin\\g++.exe";
                    else
                    {
                        Console.WriteLine("Úgy tűnik nem Code::Blocksos MinGW-t használsz, vagy szokatlan helyre telepítetted a Code::Blocksot! Kérlek add meg a g++ elérési útvonalát");

                        string g;
                        do
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            g = (Console.ReadLine() ?? "").Replace(@"""", "");
                            Console.ForegroundColor = ConsoleColor.White;
                        } while (!File.Exists(g));
                        gccpath = g;
                    }
                }

                Console.WriteLine();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Sikeres inicializálás!");
                Console.WriteLine("Mostantól, ha egy programodat akarod ellenőrizni, húzd rá az ablakra, majd üss egy entert!");
                Console.WriteLine("Ha pedig egy feladat tesztjeire vagy kíváncsi, íjr be egy kérdőjelet, majd a feladat kódszámát (pl ?2.26a)!");
                Console.ForegroundColor = ConsoleColor.White;


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
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        programpath = (Console.ReadLine() ?? "").Replace(@"""", "");
                        Console.ForegroundColor = ConsoleColor.White;
                        if (programpath.StartsWith('?'))
                        {
                            csakListazz = true;
                            programpath = programpath[1..];
                            if (programpath.EndsWith(".cpp"))
                                programpath = programpath.Substring(0, programpath.Length - 4);
                            if (!programpath.EndsWith(".plang"))
                                programpath = programpath + ".plang";
                        }

                        var filename = Path.GetFileName(programpath);
                        var sf = filename.Split(".");
                        if (sf.Length != 3) throw new Exception();
                        het = int.Parse(sf[0]);
                        feladat = sf[1];
                        if (sf[2] != "plang" && sf[2] != "cpp") throw new Exception();
                    }
                    catch
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("hibás bemenet!");
                        Console.ForegroundColor = ConsoleColor.White;
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
                        foreach ((var teszt, var valasz) in tesztek)
                        {
                            Console.WriteLine("A teszt:");
                            foreach (var sor in teszt.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine("A várt válasz:");
                            foreach (var sor in valasz.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.WriteLine("================");
                            Console.WriteLine();
                        }
                        continue;
                    }

                    foreach ((var teszt, var valasz) in tesztek)
                    {
                        string output;
                        string runError;
                        string compError;

                        if (PlangMode)
                        {
                            (output, runError) = Run_Plang(plangpath, programpath, teszt.Replace("\r", ""));
                            compError = "";
                        }
                        else
                        {
                            (output, compError, runError) = Run_Cpp(gccpath, programpath, teszt.Replace("\r", ""));
                        }

                        if (!string.IsNullOrEmpty(runError))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("========[FUTÁSIDEJŰ HIBA!]========");
                            Console.WriteLine("A teszt:");
                            foreach (var sor in teszt.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.WriteLine("A program hibája hiba:");
                            foreach (var sor in runError.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.White;

                        }
                        if (!string.IsNullOrEmpty(compError))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("=========[FORDÍTÁSI HIBA!]=========");
                            Console.WriteLine("A fordítási hiba:");
                            foreach (var sor in compError.Split("\n"))
                                Console.WriteLine("> " + sor);
                            Console.WriteLine();
                            Console.ForegroundColor = ConsoleColor.White;
                        }
                        else
                        {
                            List<string> o = new List<string>(output.TrimEnd().Split("\n"));
                            List<string> t = new List<string>(valasz.TrimEnd().Split("\n"));
                            for (int i = 0; i < o.Count; i++)
                                o[i] = o[i].TrimEnd();
                            for (int i = 0; i < t.Count; i++)
                                t[i] = t[i].TrimEnd();

                            if (Enumerable.SequenceEqual<string>(o, t))
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("=========[SIKERES TESZT!]=========");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("=========[HIBÁS VÁLASZ!]==========");
                                Console.WriteLine("A teszt:");
                                foreach (var sor in teszt.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
                                Console.WriteLine("A program válasza:");
                                foreach (var sor in output.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
                                Console.WriteLine("A várt válasz:");
                                foreach (var sor in valasz.Split("\n"))
                                    Console.WriteLine("> " + sor);
                                Console.WriteLine();
                                Console.ForegroundColor = ConsoleColor.White;
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
