using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading;
using Serilog;

namespace AgentChatAMQP
{
    class Program
    {
        static bool pollingEnabled = false;
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("server.log", rollingInterval: RollingInterval.Day).CreateLogger();
            Log.Information("Agent started.");
            Console.Title = "Agents";
            Console.ForegroundColor = ConsoleColor.DarkRed;
            try
            {
                var factory = new ConnectionFactory()
                {
                    HostName = "localhost", // RabbitMQ server address
                    UserName = "guest",
                    Password = "guest"
                };
                var connection = factory.CreateConnection();
                var agentChannel = connection.CreateModel();

                Console.WriteLine("Enter agent ID (e.g., Agent001):");
                var agentId = Console.ReadLine();

                //var customerId = "";
                Log.Information("Agent {0} started.", agentId);
                // Queue for receiving messages from the server
                agentChannel.QueueDeclare(queue: "agent_response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                // Queue for sending responses to the server
                //agentChannel.QueueDeclare(queue: "agent_response_queue", durable: false, exclusive: false, autoDelete: false, arguments: null);
                Console.WriteLine(string.Format("{0} is waiting for messages.", agentId));
                Console.WriteLine("Enter 'exit' to quit from this chat window.");
                Console.WriteLine("Enter 'close' to leave the current chat from customer.");
                var message = "";
                while (true)
                {
                    if (message.ToLower() == "exit")
                    {
                        break;
                    }
                    else if (message != "")
                    {
                        message = Console.ReadLine();
                    }
                    else
                    {
                        message = "online";
                    }
                                        
                    // Send message to the server for forwarding to the respective customer
                    var agentMessage = string.Format("{0}:{1}", agentId, message);
                    var agentMessageBody = Encoding.UTF8.GetBytes(agentMessage);
                    //Console.WriteLine(agentMessage);
                    agentChannel.BasicPublish(exchange: "", routingKey: "agent_queue", basicProperties: null, body: agentMessageBody);
                    // Wait for response from the server
                    var agentConsumer = new EventingBasicConsumer(agentChannel);
                    agentConsumer.Received += (model, ea) =>
                    {
                        var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                        Console.WriteLine(response);
                        // Inside the while loop where agent sends messages and listens for responses
                        if (response.Trim().ToLower() == "ok" && !pollingEnabled)
                        {
                            pollingEnabled = true;
                            Timer timer = new Timer(PollServer, agentChannel, TimeSpan.Zero, TimeSpan.FromSeconds(1));
                        }

                    };
                    agentChannel.BasicConsume(queue: "agent_response_queue", autoAck: true, consumer: agentConsumer);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred: {ErrorMessage}", ex.Message);
            }
                        
        }

        static void PollServer(object state)
        {
            var agentChannel = (IModel)state;
            // Implement logic to poll the server for new messages here
            // For example, you can request new messages from the server and handle the responses
            // Sample logic:
            // Request new messages from the server
            string request = "new_messages_request"; // Define a protocol for requesting new messages
            var requestBytes = Encoding.UTF8.GetBytes(request);
            agentChannel.BasicPublish(exchange: "", routingKey: "server_queue", basicProperties: null, body: requestBytes);
            // Listen for response from the server
            var consumer = new EventingBasicConsumer(agentChannel);
            consumer.Received += (model, ea) =>
            {
                var response = Encoding.UTF8.GetString(ea.Body.ToArray());
                // Handle the received message
                Console.WriteLine(string.Format("Received new message: {response}"));
            };
            agentChannel.BasicConsume(queue: "agent_response_queue", autoAck: true, consumer: consumer);
        }

    }
}
