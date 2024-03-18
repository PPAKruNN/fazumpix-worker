using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using FazUmPix.DTOs;
using System.Text;
using System.Net.Http.Json;

// RabbitMQ consumer configuration.
string queueName = "payments";
string apiUrl = "http://localhost:5000";
ConnectionFactory factory = new()
{
  HostName = "localhost",
  UserName = "rabbit",
  Password = "mq"
};

IConnection connection = factory.CreateConnection();
IModel channel = connection.CreateModel();
channel.QueueDeclare(
    queue: queueName,
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

// Consumer logic.
var client = new HttpClient
{
  BaseAddress = new Uri(apiUrl),
  Timeout = TimeSpan.FromSeconds(120)
};

Console.WriteLine("[*] Waiting for messages...");


EventingBasicConsumer consumer = new(channel);
consumer.Received += async (model, ea) =>
{
  string serialized = Encoding.UTF8.GetString(ea.Body.ToArray());
  ProcessPaymentDTO? dto = JsonSerializer.Deserialize<ProcessPaymentDTO>(serialized);

  try
  {
    if (dto == null) throw new Exception("Invalid message format!");

    // Process
    Console.WriteLine("Received message!" + dto.Data.Origin.User.CPF);

    // Webhook destiny (Process) 
    var response = await client.PostAsJsonAsync(dto.ProcessURL, dto.Data);
    if (!response.IsSuccessStatusCode) throw new Exception("Payment processing failed!");

    // Update payment status API - DATABASE.
    await client.PatchAsJsonAsync($"/Payments/{dto.PaymentId}", new
    {
      Status = "ACCEPTED"
    });

    // Webhook origin
    _ = client.PatchAsJsonAsync(dto.AcknowledgeURL, new
    {
      Id = dto.PaymentId,
      Status = "ACCEPTED"
    });

    // Respond
    Console.WriteLine("Processed payment!");
    channel.BasicAck(ea.DeliveryTag, false);

  }
  catch (Exception e)
  {
    Console.WriteLine("Ocorreu um timeout em alguma requisição");

    // Update Payment DB
    await client.PatchAsJsonAsync($"$/Payments/{dto.PaymentId}", new
    {
      Status = "REJECTED"
    });

    // Webhook origin
    await client.PatchAsJsonAsync(dto.AcknowledgeURL, new
    {
      Id = dto.PaymentId,
      Status = "REJECTED"
    });

    channel.BasicReject(ea.DeliveryTag, false);
  }
};

channel.BasicQos(0, 50, false);
channel.BasicConsume(
    queue: queueName,
    autoAck: false,
    consumer: consumer
);

Console.WriteLine("Press [enter] to exit");
Console.ReadLine();
