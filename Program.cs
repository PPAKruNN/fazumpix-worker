using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using FazUmPix.DTOs;
using System.Text;
using System.Net.Http.Json;

// RabbitMQ consumer configuration.
string queueName = "payments";
string apiUrl = "http://localhost:5000/Payments";

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
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null
);

var httpClient = new HttpClient();

// Worker logic.
Console.WriteLine("[*] Waiting for messages...");

EventingBasicConsumer consumer = new(channel);
consumer.Received += async (model, ea) =>
{
  string serialized = Encoding.UTF8.GetString(ea.Body.ToArray());
  ProcessPaymentDTO? dto = JsonSerializer.Deserialize<ProcessPaymentDTO>(serialized);
  if (dto == null)
  {
    channel.BasicReject(ea.DeliveryTag, false);
    throw new Exception("Invalid message format!");
  }

  try
  {
    var twoMinuteHttpClient = new HttpClient
    {
      Timeout = TimeSpan.FromSeconds(120)
    };

    // Process payment on destiny.
    Console.WriteLine("Received message!" + dto.Data.Origin.User.CPF);
    var response = await twoMinuteHttpClient.PostAsJsonAsync(dto.ProcessURL, dto.Data);
    if (!response.IsSuccessStatusCode) throw new Exception("Processing failed!");

    // Update payment status on database.
    Console.WriteLine("Received message!" + dto.Data.Origin.User.CPF);
    await httpClient.PatchAsJsonAsync($"{apiUrl}/{dto.PaymentId}", new { Status = "ACCEPTED" });

    // Webhook for origin psp, and ack message.
    _ = httpClient.PatchAsJsonAsync(dto.AcknowledgeURL, new
    {
      Id = dto.PaymentId,
      Status = "ACCEPTED"
    });

    channel.BasicAck(ea.DeliveryTag, false);
    Console.WriteLine("Processed payment!");
  }
  catch (Exception e)
  {
    Console.WriteLine(e.Message);

    // Update payment status on database.
    await httpClient.PatchAsJsonAsync($"{apiUrl}/{dto.PaymentId}", new { Status = "REJECTED" });

    // Webhook for origin psp, and Nack message.
    _ = httpClient.PatchAsJsonAsync(dto.AcknowledgeURL, new
    {
      Id = dto.PaymentId,
      Status = "REJECTED"
    });

    channel.BasicReject(ea.DeliveryTag, false);
  }
};

channel.BasicConsume(
    queue: queueName,
    autoAck: false,
    consumer: consumer
);

Console.WriteLine("Press [enter] to exit");
Console.ReadLine();
