// HP Revit MCP - add-in chinh
// Cong dung : Mo cong 127.0.0.1:9877 trong Revit de Claude dieu khien Revit qua MCP.
//             Lenh gui dang JSON 1 dong; Revit thuc hien tren luong chinh (ExternalEvent).
// An toan   : Chi nghe 127.0.0.1. KHONG co lenh xoa. Moi lenh ghi nam trong 1 Transaction (Ctrl+Z hoan tac duoc).
// Ngay viet : 2026-11-21
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Autodesk.Revit.UI;

namespace HPRevitMcp
{
    public class App : IExternalApplication
    {
        public const string Version = "1.0.0";
        public const int Port = 9877;

        internal sealed class Job
        {
            public string Method;
            public Dictionary<string, object> Params;
            public ManualResetEventSlim Done = new ManualResetEventSlim(false);
            public object Result;
            public string Error;
        }

        private static readonly ConcurrentQueue<Job> Queue = new ConcurrentQueue<Job>();
        private static ExternalEvent _event;
        private static TcpListener _listener;
        private static volatile bool _stop;

        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                _event = ExternalEvent.Create(new Handler());
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                var th = new Thread(AcceptLoop) { IsBackground = true, Name = "HPRevitMcp-accept" };
                th.Start();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("HP Revit MCP", "Khong mo duoc cong " + Port + ": " + ex.Message);
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            _stop = true;
            try { _listener?.Stop(); } catch { }
            return Result.Succeeded;
        }

        private static void AcceptLoop()
        {
            while (!_stop)
            {
                TcpClient c;
                try { c = _listener.AcceptTcpClient(); }
                catch { break; }
                var t = new Thread(() => ClientLoop(c)) { IsBackground = true, Name = "HPRevitMcp-client" };
                t.Start();
            }
        }

        private static void ClientLoop(TcpClient c)
        {
            using (c)
            {
                try
                {
                    var stream = c.GetStream();
                    var reader = new StreamReader(stream, new UTF8Encoding(false));
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Trim().Length == 0) continue;
                        string resp = Handle(line);
                        var bytes = new UTF8Encoding(false).GetBytes(resp + "\n");
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                    }
                }
                catch { }
            }
        }

        private static string Handle(string line)
        {
            object id = null;
            try
            {
                var req = (Dictionary<string, object>)Json.Parse(line);
                req.TryGetValue("id", out id);
                var job = new Job { Method = Convert.ToString(req["method"]) };
                job.Params = req.ContainsKey("params") && req["params"] is Dictionary<string, object> p
                    ? p : new Dictionary<string, object>();
                Queue.Enqueue(job);
                _event.Raise();
                if (!job.Done.Wait(TimeSpan.FromSeconds(120)))
                    throw new TimeoutException("Revit khong tra loi trong 120 giay (co the dang mo hop thoai - hay dong hop thoai).");
                if (job.Error != null)
                    return Json.Write(new Dictionary<string, object> { { "id", id }, { "ok", false }, { "error", job.Error } });
                return Json.Write(new Dictionary<string, object> { { "id", id }, { "ok", true }, { "result", job.Result } });
            }
            catch (Exception ex)
            {
                return Json.Write(new Dictionary<string, object> { { "id", id }, { "ok", false }, { "error", ex.GetType().Name + ": " + ex.Message } });
            }
        }

        private sealed class Handler : IExternalEventHandler
        {
            public string GetName() { return "HP Revit MCP"; }

            public void Execute(UIApplication uiapp)
            {
                Job job;
                while (Queue.TryDequeue(out job))
                {
                    try { job.Result = Commands.Run(uiapp, job.Method, job.Params); }
                    catch (Exception ex) { job.Error = ex.GetType().Name + ": " + ex.Message; }
                    finally { job.Done.Set(); }
                }
            }
        }
    }
}
