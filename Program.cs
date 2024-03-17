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


// talvez seja interessante reduzir o throughput.
EventingBasicConsumer consumer = new(channel);
consumer.Received += (model, ea) =>
{
  string serialized = Encoding.UTF8.GetString(ea.Body.ToArray());
  ProcessPaymentDTO? dto = JsonSerializer.Deserialize<ProcessPaymentDTO>(serialized);

  try
  {
    if (dto == null) throw new Exception("Invalid message format!");

    // Process
    Console.WriteLine("Received message!" + dto.Data.Origin.User.CPF);
    var response = client.PostAsJsonAsync(dto.ProcessURL, dto.Data);
    response.Wait();

    if (!response.Result.IsSuccessStatusCode) throw new Exception("Processing failed!");

    var r1 = client.PatchAsJsonAsync($"/Payments/{dto.PaymentId}", new
    {
      Status = "ACCEPTED"
    });

    r1.Wait();

    client.PatchAsJsonAsync(dto.AcknowledgeURL, new
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
    Console.WriteLine(e.Message);

    // ISSO AQUI PODE FALHAR POR CONTA DO MOCK!
    client.PatchAsJsonAsync($"$/Payments/{dto.PaymentId}", new
    {
      Status = "REJECTED"
    });

    client.PatchAsJsonAsync(dto.AcknowledgeURL, new
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
