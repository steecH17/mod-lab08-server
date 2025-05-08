using System;
using System.Threading;
using System.IO;
using System.Text;

namespace MultiChannelCMO
{
    class Program
    {
        static void Main(string[] args)
        {
            RunExperiments();
            Console.WriteLine("Эксперименты проведены успешно! Данные записаны в файл data.txt");

            // Вывод результатов
            // Console.WriteLine("\nРезультаты моделирования:");
            // Console.WriteLine($"Всего заявок: {server.RequestCount}");
            // Console.WriteLine($"Обслужено: {server.ProcessedCount} (эксп. Q = {expQ:F4})");
            // Console.WriteLine($"Отклонено: {server.RejectedCount} (эксп. Pn = {expPn:F4})");

            // Console.WriteLine("\nСравнение показателей:");
            // Console.WriteLine("| Показатель               | Теория  | Эксперимент | Отклонение |");
            // Console.WriteLine("|--------------------------|---------|-------------|------------|");
            // PrintComparison("Вероятность простоя (P0)", P0, expP0);
            // PrintComparison("Вероятность отказа (Pn)", Pn, expPn);
            // PrintComparison("Отн. пропуск. способ. (Q)", Q, expQ);
            // PrintComparison("Абс. пропуск. способ. (A)", A, expA);
            // PrintComparison("Ср. число занятых каналов (k)", k, expK);
            // SaveResultsToFile(lambda, mu, 
            //                     P0, Pn, Q, A, k,
            //                     expP0, expPn, expQ, expA, expK);
        }

        static void RunExperiments()
        {
            const double mu = 1.0; // Фиксированная интенсивность обслуживания
            const int n = 5; // Количество каналов
            const int totalRequests = 20; // Количество заявок 
            const double minLambda = 0.5;
            const double maxLambda = 8.0;
            const double lambdaStep = 0.5;

            for (double lambda = minLambda; lambda <= maxLambda; lambda += lambdaStep)
            {
                Console.WriteLine($"\nЭксперимент: λ = {lambda:F1}, μ = {mu:F1}");

                Server server = new Server(n, mu);
                Client client = new Client(server);

                for (int id = 1; id <= totalRequests; id++)
                {
                    client.Send(id);
                    Thread.Sleep((int)(1000 / lambda));
                }

                while (server.GetBusyChannelsCount() > 0)
                {
                    Thread.Sleep(100);
                }

                double rho = lambda / mu;
                double P0 = CalculateP0(rho, n);
                double Pn = (Math.Pow(rho, n) / Factorial(n)) * P0;
                double Q = 1 - Pn;
                double A = lambda * Q;
                double k = rho * (1 - Pn);

                double expP0 = (double)server.IdleTime / server.TotalTime;
                double expPn = (double)server.RejectedCount / server.RequestCount;
                double expQ = (double)server.ProcessedCount / server.RequestCount;
                double expA = lambda * expQ;
                double expK = server.TotalBusyTime / (server.TotalTime * mu);

                SaveResultsToFile(lambda, mu, 
                                P0, Pn, Q, A, k,
                                expP0, expPn, expQ, expA, expK);
            }
        }

        static void SaveResultsToFile(double lambda, double mu,
                                    double P0, double Pn, double Q, double A, double k,
                                    double expP0, double expPn, double expQ, double expA, double expK)
        {
            string data = $"{lambda} {mu} {P0:F4} {Pn:F4} {Q:F4} {A:F4} {k:F4} " +
                         $"{expP0:F4} {expPn:F4} {expQ:F4} {expA:F4} {expK:F4}";

            string dataPath = Path.Combine(Environment.CurrentDirectory, "data.txt");

            File.AppendAllText(dataPath, data + Environment.NewLine);
        }

        static void PrintComparison(string name, double theory, double experiment)
        {
            Console.WriteLine($"| {name,-25} | {theory:F4}  | {experiment:F4}     | {(theory-experiment):F4}    |");
        }

        static double CalculateP0(double rho, int n)
        {
            double sum = 0;
            for (int i = 0; i <= n; i++)
            {
                sum += Math.Pow(rho, i) / Factorial(i);
            }
            return 1 / sum;
        }

        static double Factorial(int k)
        {
            if (k <= 1) return 1;
            return k * Factorial(k - 1);
        }
    }

    public class Server
    {
        private PoolRecord[] pool;
        private object statsLock = new object();
        private DateTime[] startTimes;
        public int RequestCount { get; private set; }
        public int ProcessedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public double TotalBusyTime { get; private set; }
        public double IdleTime { get; private set; }
        public double TotalTime { get; private set; }
        private DateTime systemStartTime;
        private double mu;

        public Server(int poolSize, double serviceRate)
        {
            pool = new PoolRecord[poolSize];
            startTimes = new DateTime[poolSize];
            mu = serviceRate;
            systemStartTime = DateTime.Now;
        }

        public void ProcessRequest(object? sender, ProcEventArgs e)
        {
            lock (statsLock)
            {
                RequestCount++;
                TotalTime = (DateTime.Now - systemStartTime).TotalSeconds;
                Console.WriteLine($"[{DateTime.Now:T}] Заявка #{e.Id} поступила на сервер");

                if (GetBusyChannelsCount() == 0)
                    IdleTime += (DateTime.Now - systemStartTime).TotalSeconds - TotalTime;

                for (int i = 0; i < pool.Length; i++)
                {
                    if (!pool[i].InUse)
                    {
                        pool[i].InUse = true;
                        pool[i].Thread = new Thread(HandleRequest!);
                        startTimes[i] = DateTime.Now;
                        pool[i].Thread.Start(e.Id);
                        ProcessedCount++;
                         Console.WriteLine($"[{DateTime.Now:T}] Заявка #{e.Id} принята в канал {i+1}");
                        return;
                    }
                }
                RejectedCount++;
                Console.WriteLine($"[{DateTime.Now:T}] Заявка #{e.Id} отклонена (нет свободных каналов)");
            }
        }

        private void HandleRequest(object? idObj)
        {
            if (idObj == null) return;

            int id = (int)idObj;
            int channelIndex = -1;
            Console.WriteLine($"[{DateTime.Now:T}] Начата обработка заявки #{id}");
            
            lock (statsLock)
            {
                for (int i = 0; i < pool.Length; i++)
                {
                    if (pool[i].Thread == Thread.CurrentThread)
                    {
                        channelIndex = i;
                        break;
                    }
                }
            }

            Thread.Sleep((int)(1000 / mu));

            lock (statsLock)
            {
                if (channelIndex >= 0)
                {
                    TotalBusyTime += (DateTime.Now - startTimes[channelIndex]).TotalSeconds;
                    pool[channelIndex].InUse = false;
                    Console.WriteLine($"[{DateTime.Now:T}] Заявка #{id} успешно обработана в канале {channelIndex}");
                }
            }
        }

        public int GetBusyChannelsCount()
        {
            int count = 0;
            foreach (var record in pool)
            {
                if (record.InUse) count++;
            }
            return count;
        }
    }

    public class Client
    {
        private Server server;
        
        public Client(Server server)
        {
            this.server = server;
            this.Request += server.ProcessRequest;
        }

        public void Send(int id)
        {
            ProcEventArgs args = new ProcEventArgs { Id = id };
            OnRequest(args);
        }

        protected virtual void OnRequest(ProcEventArgs e)
        {
            Request?.Invoke(this, e);
        }

        public event EventHandler<ProcEventArgs>? Request;
    }

    public class ProcEventArgs : EventArgs
    {
        public int Id { get; set; }
    }

    struct PoolRecord
    {
        public Thread? Thread;
        public bool InUse;
    }
}