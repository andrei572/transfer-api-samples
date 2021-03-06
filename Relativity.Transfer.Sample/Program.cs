﻿// ----------------------------------------------------------------------------
// <copyright file="Program.cs" company="kCura Corp">
//   kCura Corp (C) 2017 All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Transfer
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;

    using Relativity.Logging;

    /// <summary>
    /// Represents a sample console application to demo basic Transfer API usage.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        /// <param name="args">
        /// The arguments.
        /// </param>
        public static void Main(string[] args)
        {
            // Suppress SSL validation errors.
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) => true;

            try
            {
                // Setup global logging parameters for all transfers.
                LogSettings.Instance.ApplicationName = "Sample App";
                LogSettings.Instance.LogIntervalSeconds = 1;
                LogSettings.Instance.MinimumLogLevel = LoggingLevel.Debug;

                // Enabling this setting automatically logs useful transfer statitistics.
                LogSettings.Instance.StatisticsLogEnabled = true;
                LogSettings.Instance.StatisticsLogIntervalSeconds = 1;

                // Using a custom transfer log to send all entries to Serilog.
                using (ITransferLog transferLog = new CustomTransferLog())
                {
                    ExecuteUploadDemo(transferLog);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("A serious transfer failure has occurred. Error: " + e);
            }
            finally
            {
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Executes an upload demo.
        /// </summary>
        /// <param name="transferLog">
        /// The custom transfer log.
        /// </param>
        private static async void ExecuteUploadDemo(ITransferLog transferLog)
        {
            // The context object is used to decouple operations such as progress from other TAPI objects.
            TransferContext context = new TransferContext { StatisticsRateSeconds = .5 };
            context.TransferPathIssue += (sender, args) =>
                {
                    Console.WriteLine($"*** The transfer error has occurred. Attributes={args.Issue.Attributes}");
                };

            context.TransferRequest += (sender, args) =>
                {
                    Console.WriteLine($"*** The transfer request {args.Request.ClientRequestId} status has changed. Status={args.Status}");
                };

            context.TransferPathProgress += (sender, args) =>
                {
                    if (args.Status == TransferPathStatus.Successful)
                    {
                        Console.WriteLine($"*** The source file '{args.Path.SourcePath}' transfer is successful.");
                    }
                };

            context.TransferJobRetry += (sender, args) =>
                {
                    Console.WriteLine($"*** Job {args.Request.ClientRequestId} is retrying. Retry={args.Count}");
                };

            context.TransferStatistics += (sender, args) =>
                {
                    Console.WriteLine($"*** Progress: {args.Statistics.Progress:00.00}%, Transfer rate: {args.Statistics.TransferRateMbps:00.00} Mbps");
                };

            // The CancellationTokenSource is used to cancel the transfer operation.
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            //// cancellationTokenSource.Cancel();

            // TODO: Update with the Relativity instance, credentials, and workspace.
            Uri relativityHost = new Uri("https://relativity_host.com/Relativity");
            IHttpCredential credential = new BasicAuthenticationCredential("jsmith@example.com", "UnbreakableP@ssword777");
            int workspaceId = 1027428;
            
            // The CreateClientAsync method chooses the best client at runtime.
            using (IRelativityTransferHost host = new RelativityTransferHost(new RelativityConnectionInfo(relativityHost, credential, workspaceId), transferLog))
            using (ITransferClient client = await host.CreateClientAsync(cancellationTokenSource.Token))
            {
                // Display a friendly name for the client that was just created.
                Console.WriteLine($"Client {client.DisplayName} has been created.");

                // Retrieve workspace details in order to specify a UNC path.
                var workspace = await client.GetWorkspaceAsync(cancellationTokenSource.Token);
                var targetPath = Path.Combine(workspace.DefaultFileShareUncPath + @"\UploadDataset");
                TransferRequest uploadRequest = TransferRequest.ForUploadJob(targetPath, context);

                // Once the job is created, an asynchronous queue is available to add paths and perform immediate transfers.
                using (var job = await client.CreateJobAsync(uploadRequest, cancellationTokenSource.Token))
                {
                    // TODO: Update this collection with valid source paths to upload.
                    job.AddPaths(new[] { new TransferPath {SourcePath = @"C:\Datasets\transfer-api-sample\sample.pdf"} });

                    // Await completion of the job up to the specified max time period. Events will continue to provide feedback.
                    ITransferResult uploadResult = await job.CompleteAsync(cancellationTokenSource.Token);
                    Console.WriteLine($"Upload transfer result: {uploadResult.Status}, Files: {uploadResult.TotalTransferredFiles}");
                    Console.WriteLine($"Upload transfer data rate: {uploadResult.TransferRateMbps:#.##} Mbps");
                    Console.WriteLine("Press ENTER to terminate.");
                }
            }
        }
    }
}