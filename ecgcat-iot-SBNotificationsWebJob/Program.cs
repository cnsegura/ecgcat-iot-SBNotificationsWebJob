using Microsoft.Azure.NotificationHubs;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Configuration;
using System.IO;
using System.Threading;

namespace ecgcat_iot_SBNotificationsWebJob
{
    class Program
    {
        
        static void Main(string[] args)
        {
            //sanity check. Should not need this code if the connection string is correct
            if (!VerifyConfiguration())
            {
                Console.ReadLine();
                return;
            }
            CreateSubsciption();
            ReceiveAndNotify();
                        
        }

        private static bool VerifyConfiguration()
        {
            bool configOK = true;
            var connectionString = ConfigurationManager.AppSettings["Microsoft.ServiceBus.ConnectionString"];
            if (connectionString.Contains("[your namespace]") || connectionString.Contains("[your access key]"))
            {
                configOK = false;
                Console.WriteLine("Please update the 'Microsoft.ServiceBus.ConnectionString' appSetting in app.config to specify your Service Bus namespace and secret key.");
            }
            return configOK;

        }
        private static void CreateSubsciption()
        {
            Microsoft.ServiceBus.NamespaceManager namespaceManager = Microsoft.ServiceBus.NamespaceManager.Create();
            TopicDescription myTopic = namespaceManager.GetTopic("tempdata");
           // SubscriptionDescription myAgentSubscription = namespaceManager.CreateSubscription(myTopic.Path, "AgentSubscription");

        }
        private static void ReceiveAndNotify()
        {
            SubscriptionClient agentSubscriptionClient = SubscriptionClient.Create("tempdata", "AgentSubscription");
            BrokeredMessage message = null;

            var toastMessage = @"<toast><visual><binding template=""ToastGeneric""><text id=""1"">{messagepayload}</text></binding></visual><actions><action activationType=""foreground"" content=""Details"" arguments=""details"" />< action activationType = ""background"" content = ""Attempt Fix"" arguments = ""attemptfix"" /></ actions ></toast>";

            while (true)
            {
                try
                {
                    //receive messages from Agent Subscription
                    message = agentSubscriptionClient.Receive();           
                       
                    if (message != null)
                    {
                        Console.WriteLine("\nReceiving message from AgentSubscription...");

                        //The Storm bolt converts string to stream. Need to convert back.
                        var stream = message.GetBody<Stream>();
                        StreamReader reader = new StreamReader(stream);
                        string messageBody = reader.ReadToEnd();
                        
                        toastMessage = toastMessage.Replace("{messagepayload}", messageBody);
                        //Send notification
                        SendNotificationAsync(toastMessage);

                        // Remove message from subscription
                        message.Complete();
                    }
                    else
                    {
                        //no more messages in the subscription
                        break;
                    }
                }
                catch (MessagingException e)
                {
                    if (!e.IsTransient)
                    {
                        Console.WriteLine(e.Message);
                        throw;
                    }
                    else
                    {
                        HandleTransientErrors(e);
                    }
                }
            }
        }

        private static async void SendNotificationAsync(string message)
        {
            var notificationHubConnection = ConfigurationManager.AppSettings["Microsoft.Notificationhub.ConnectionString"];
            NotificationHubClient hub = NotificationHubClient.CreateClientFromConnectionString(notificationHubConnection, "ecgcat-iot");
            await hub.SendWindowsNativeNotificationAsync(message);
        }
        private static void HandleTransientErrors(MessagingException e)
        {
            //If transient error/exception, let's back-off for 2 seconds and retry
            Console.WriteLine(e.Message);
            Console.WriteLine("Will retry sending the message in 2 seconds");
            Thread.Sleep(2000);
        }
    }
}
