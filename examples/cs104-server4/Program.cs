using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using lib60870;
using lib60870.CS101;
using lib60870.CS104;


namespace cs104_server4
{
    class MainProgram
    {
        public static void Main(string[] args)
        {
            bool running = true;

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                running = false;
            };

            // specify application layer parameters (CS 101 and CS 104)
            var alParams = new ApplicationLayerParameters();
            alParams.SizeOfCA = 2;
            alParams.SizeOfIOA = 3;
            alParams.MaxAsduLength = 249;

            // specify APCI parameters (only CS 104)
            var apciParameters = new APCIParameters();
            apciParameters.K = 12;
            apciParameters.W = 8;
            apciParameters.T0 = 10;
            apciParameters.T1 = 15;
            apciParameters.T2 = 10;
            apciParameters.T3 = 20;

            Server server = new Server(apciParameters, alParams);

            server.DebugOutput = true;

            server.MaxQueueSize = 10;
            server.EnqueueMode = EnqueueMode.REMOVE_OLDEST;

            server.Start();

            ASDU newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.INITIALIZED, false, false, 0, 1, false);
            EndOfInitialization eoi = new EndOfInitialization(0);
            newAsdu.AddInformationObject(eoi);
            server.EnqueueASDU(newAsdu);

            int waitTime = 1000;

            while (running && server.IsRunning())
            {
                Thread.Sleep(100);

                if (waitTime > 0)
                    waitTime -= 100;
                else
                {

                    newAsdu = new ASDU(server.GetApplicationLayerParameters(), CauseOfTransmission.PERIODIC, false, false, 0, 1, false);

                    newAsdu.AddInformationObject(new MeasuredValueScaled(110, -1, new QualityDescriptor()));

                    server.EnqueueASDU(newAsdu);

                    RedundancyGroup redundancyGroup = new RedundancyGroup();
                    int count = server.GetNumberOfQueueEntries(redundancyGroup);
                    Console.WriteLine($"Number of queue entries: {count}");

                    waitTime = 1000;
                }
            }

            if (server.IsRunning())
            {
                Console.WriteLine("Stop server");
                server.Stop();
            }
            else
            {
                Console.WriteLine("Server stopped");
            }

        }
    }
}
