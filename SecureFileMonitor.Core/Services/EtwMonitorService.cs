using System;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using SecureFileMonitor.Core.Models;
using Serilog;

namespace SecureFileMonitor.Core.Services
{
    public class EtwMonitorService : IEtwMonitorService, IDisposable
    {
        private TraceEventSession? _session;
        private readonly string _sessionName = "SecureFileMonitorSession";
        private Task? _processingTask;
        private bool _isDisposed;

        public event EventHandler<FileActivityEvent>? OnFileActivity;
        public bool IgnoreInternalDatabase { get; set; } = true;

        private bool IsInternalDatabase(string path)
        {
            // Simple check for now. Could be more robust by checking exact path of DB.
            return path.Contains("secure_monitor.db", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("log.txt", StringComparison.OrdinalIgnoreCase); 
        }

        public void Start()
        {
            if (_session != null) return;

            // Run in a separate task because Process() blocks
            _processingTask = Task.Run(() =>
            {
                try
                {
                    // Note: Requires Admin
                    using (_session = new TraceEventSession(_sessionName))
                    {
                        // Enable Kernel Provider
                        _session.EnableKernelProvider(
                            KernelTraceEventParser.Keywords.FileIO |
                            KernelTraceEventParser.Keywords.FileIOInit | 
                            KernelTraceEventParser.Keywords.Process);

                        _session.Source.Kernel.FileIOCreate += (data) =>
                        {
                            if (string.IsNullOrEmpty(data.FileName)) return;
                            if (IgnoreInternalDatabase && IsInternalDatabase(data.FileName)) return;

                            // Filter system files if needed, but for now capture all
                            OnFileActivity?.Invoke(this, new FileActivityEvent
                            {
                                Timestamp = data.TimeStamp,
                                ProcessId = data.ProcessID,
                                ProcessName = data.ProcessName,
                                FilePath = data.FileName,
                                Operation = FileOperation.Create,
                                UserName = Environment.UserName 
                            });
                        };

                        _session.Source.Kernel.FileIOWrite += (data) =>
                        {
                            if (string.IsNullOrEmpty(data.FileName)) return;
                            if (IgnoreInternalDatabase && IsInternalDatabase(data.FileName)) return;

                            OnFileActivity?.Invoke(this, new FileActivityEvent
                            {
                                Timestamp = data.TimeStamp,
                                ProcessId = data.ProcessID,
                                ProcessName = data.ProcessName,
                                FilePath = data.FileName,
                                Operation = FileOperation.Write,
                                UserName = Environment.UserName
                            });
                        };
                        
                        _session.Source.Kernel.FileIODelete += (data) =>
                        {
                            if (string.IsNullOrEmpty(data.FileName)) return;
                            if (IgnoreInternalDatabase && IsInternalDatabase(data.FileName)) return;

                             OnFileActivity?.Invoke(this, new FileActivityEvent
                            {
                                Timestamp = data.TimeStamp,
                                ProcessId = data.ProcessID,
                                ProcessName = data.ProcessName,
                                FilePath = data.FileName,
                                Operation = FileOperation.Delete,
                                UserName = Environment.UserName
                            });
                        };

                        _session.Source.Process();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ETW Session failed");
                }
                finally
                {
                    _session = null;
                }
            });
        }

        public void Stop()
        {
            if (_session != null && _session.IsActive)
            {
                _session.Stop();
                // Wait for task?
            }
        }

        public void Dispose()
        {
           Dispose(true);
           GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Stop();
                }
                _isDisposed = true;
            }
        }
    }
}
