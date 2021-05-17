using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using Quickstarts.ReferenceServer;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPCUAMock
{
    class Program
    {
        private static ReferenceServer m_server;
        private static Task m_status;
        private static DateTime m_lastEventTime;
        public static bool LogConsole { get; set; } = false;
        public static bool AutoAccept { get; set; } = false;
        public static string Password { get; set; } = null;

        static async Task Main(string[] args)
        {
            CertificatePasswordProvider PasswordProvider = new CertificatePasswordProvider(Password);
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OPCUA Mock",
                ApplicationType = ApplicationType.Server,
               // ConfigSectionName = Utils.IsRunningOnMono() ? "Quickstarts.MonoReferenceServer" : "Quickstarts.ReferenceServer",
                CertificatePasswordProvider = PasswordProvider
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false).ConfigureAwait(false);

            var loggerConfiguration = new Serilog.LoggerConfiguration();
            if (LogConsole)
            {
                loggerConfiguration.WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning);
            }


            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(
                false, CertificateFactory.DefaultKeySize, CertificateFactory.DefaultLifeTime).ConfigureAwait(false);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (!config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
            {
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }

            var appsettings = AppSettings.Load("Settings/Appsettings.json");
            // start the server.
            m_server = new ReferenceServer(appsettings);
            await application.Start(m_server).ConfigureAwait(false);

            // print endpoint info
            var endpoints = application.Server.GetEndpoints().Select(e => e.EndpointUrl).Distinct();
            foreach (var endpoint in endpoints)
            {
                Console.WriteLine(endpoint);
            }

            // start the status thread
            m_status = Task.Run(new Action(StatusThreadAsync));

            // print notification on session events
            m_server.CurrentInstance.SessionManager.SessionActivated += EventStatus;
            m_server.CurrentInstance.SessionManager.SessionClosing += EventStatus;
            m_server.CurrentInstance.SessionManager.SessionCreated += EventStatus;

            while(true)
            {
                await Task.Delay(1000);
            }
        }

        private static async void StatusThreadAsync()
        {
            while (m_server != null)
            {
                if (DateTime.UtcNow - m_lastEventTime > TimeSpan.FromMilliseconds(6000))
                {
                    IList<Session> sessions = m_server.CurrentInstance.SessionManager.GetSessions();
                    for (int ii = 0; ii < sessions.Count; ii++)
                    {
                        Session session = sessions[ii];
                        PrintSessionStatus(session, "-Status-", true);
                    }
                    m_lastEventTime = DateTime.UtcNow;
                }
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }

        private static void PrintSessionStatus(Session session, string reason, bool lastContact = false)
        {
            lock (session.DiagnosticsLock)
            {
                StringBuilder item = new StringBuilder();
                item.AppendFormat("{0,9}:{1,20}:", reason, session.SessionDiagnostics.SessionName);
                if (lastContact)
                {
                    item.AppendFormat("Last Event:{0:HH:mm:ss}", session.SessionDiagnostics.ClientLastContactTime.ToLocalTime());
                }
                else
                {
                    if (session.Identity != null)
                    {
                        item.AppendFormat(":{0,20}", session.Identity.DisplayName);
                    }
                    item.AppendFormat(":{0}", session.Id);
                }
                Console.WriteLine(item.ToString());
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                if (AutoAccept)
                {
                    if (!LogConsole)
                    {
                        Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                    }
                    Utils.Trace(Utils.TraceMasks.Security, "Accepted Certificate: {0}", e.Certificate.Subject);
                    e.Accept = true;
                    return;
                }
            }
            if (!LogConsole)
            {
                Console.WriteLine("Rejected Certificate: {0} {1}", e.Error, e.Certificate.Subject);
            }
            Utils.Trace(Utils.TraceMasks.Security, "Rejected Certificate: {0} {1}", e.Error, e.Certificate.Subject);
        }

        private static void EventStatus(Session session, SessionEventReason reason)
        {
            m_lastEventTime = DateTime.UtcNow;
            PrintSessionStatus(session, reason.ToString());
        }
    }
}
