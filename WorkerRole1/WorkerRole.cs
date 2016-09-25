using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.ServiceBus.Messaging;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Configuration;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("WorkerRole1 is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // 同時接続の最大数を設定します
            ServicePointManager.DefaultConnectionLimit = 12;

            // 構成の変更を処理する方法については、
            // MSDN トピック (http://go.microsoft.com/fwlink/?LinkId=166357) を参照してください。

            bool result = base.OnStart();

            Trace.TraceInformation("WorkerRole1 has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("WorkerRole1 is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("WorkerRole1 has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            //App.Configのパラメータを取得してます。
            string eventHubName = ConfigurationManager.AppSettings["Microsoft.ServiceBus.EventHubToUse"];
            string connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ServiceBusConnectionString"];
            string BaseAddress = ConfigurationManager.AppSettings["BaseAddress"];
            string URIAddress = ConfigurationManager.AppSettings["URIAddress"];
            string Username = ConfigurationManager.AppSettings["UserName"];
            string Password = ConfigurationManager.AppSettings["Password"];
            int SleepTimeMs = Int32.Parse(ConfigurationManager.AppSettings["SleepTimeMs"]);

            while (!cancellationToken.IsCancellationRequested)
            {
                Trace.TraceInformation("Working");
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(BaseAddress);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    //ベーシック認証に対応しています。ユーザー名が空欄の場合は認証ヘッダーに追加されません。
                    if(Username != null)
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(Username + ":" + Password)));
                    }

                    //指定したAPIからメッセージを受信する処理です。
                    HttpResponseMessage response = await client.GetAsync(URIAddress);
                    if (response.IsSuccessStatusCode)
                    {
                        var message = await response.Content.ReadAsStringAsync();
                        var eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, eventHubName);
                        try
                        {
                            //Event Hubsへのメッセージ送信処理です。
                            eventHubClient.Send(new EventData(Encoding.UTF8.GetBytes(message)));
                        }
                        catch (Exception exception)
                        {
                        }
                    }
                }


                await Task.Delay(60000);
            }
        }
    }
}
