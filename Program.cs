using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

// 命令列參數使用參考 https://blog.darkthread.net/blog/-net6-args-parsing/
IConfiguration config = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();
int photoCount = int.Parse(config.GetValue<string>("photoCount", "100"));
int queueSize = int.Parse(config.GetValue<string>("queueSize", "10"));
int producerCount = int.Parse(config.GetValue<string>("producerCount", "1"));
int imgProcTime =  int.Parse(config.GetValue<string>("imgProcTime", "4000"));;
int saveDbTime =  int.Parse(config.GetValue<string>("saveDbTime", "1000"));;

var photoIds = new ConcurrentQueue<int>(Enumerable.Range(1, photoCount));
var blockQueue = new BlockingCollection<SimuImgData>(queueSize);
var rnd = new Random();

var sw = new Stopwatch();
sw.Start();
var producerTasks = Enumerable.Range(1, producerCount)
    .Select(o => Task.Run(RunProducer)).ToArray();
var consumerTask = Task.Run(RunConsumer);

Task.WaitAll(producerTasks);
//生產者已完成所有工作，宣告 blockQueue 不再新增項目
blockQueue.CompleteAdding();
//等待消費者作業完成
consumerTask.Wait();

// 延遲指定時間(ms) 含 ±20% 隨機變化
void RandomDelay(int baseTime) {
    Thread.Sleep(baseTime * 4 / 5 + rnd.Next(baseTime * 2 / 5));
}
void Print(string msg, ConsoleColor color = ConsoleColor.White) {
    Console.ForegroundColor = color;
    Console.WriteLine(msg);
    Console.ResetColor();
}

void RunProducer() {
    while (photoIds.Any()) {
        if (photoIds.TryDequeue(out var id)) {
            //模擬照片縮放、縮圖作業延遲
            RandomDelay(imgProcTime);
            //若達到queueSize上限，Add動作會被Block住
            blockQueue.Add(new SimuImgData{
                Id = id,
                Image = Array.Empty<byte>()
            });
            Print($"{DateTime.Now:mm:ss} Photo {id} is processed", ConsoleColor.Yellow);
        }
    }
}

void RunConsumer()
{
    //IsComplete 表示生產者已不再新增資料，blockQueue 的資料也消化完畢
    while (!blockQueue.IsCompleted) 
    {
        SimuImgData data = null!;
        try {
            //若blockQueue沒有資料，Take動作會被Block住，有資料時再往下執行
            //若
            data = blockQueue.Take();
        } catch (InvalidOperationException) {}
        if (data != null) {
            //模擬寫入資料庫延遲
            RandomDelay(saveDbTime);
            Print($"{DateTime.Now:mm:ss} Photo {data.Id} is saved to DB", ConsoleColor.Cyan);
        }
    }
    sw.Stop();
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Done {sw.ElapsedMilliseconds:n0}ms");
}

public class SimuImgData
{
    public int Id { get; set; }
    public byte[] Image { get; set; } = null!;
}
