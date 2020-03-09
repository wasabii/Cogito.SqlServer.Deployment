﻿using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Configures the instance as a distributor.
    /// </summary>
    public class SqlDeploymentDistributorStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        public SqlDeploymentDistributorStep(string instanceName) :
            base(instanceName)
        {

        }

        /// <summary>
        /// Gets the name of the distribution database to create.
        /// </summary>
        public string DatabaseName { get; internal set; }

        /// <summary>
        /// Gets the admin password to be configured on the distributor.
        /// </summary>
        public string AdminPassword { get; internal set; }

        /// <summary>
        /// Gets the path of the distribution database data files.
        /// </summary>
        public string DataPath { get; internal set; }

        /// <summary>
        /// Gets the path of the distribution database log files.
        /// </summary>
        public string LogsPath { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public int? LogFileSize { get; internal set; } = 2;

        public int? MinimumRetention { get; internal set; } = 0;

        public int? MaximumRetention { get; internal set; } = 72;

        public int? HistoryRetention { get; internal set; } = 48;

        public string SnapshotPath { get; internal set; }

        public override async Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            var distributorName = (string)await cnn.ExecuteScalarAsync("SELECT name FROM sys.servers WHERE is_distributor = 1");
            return distributorName == null;
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            await ConfigureDistributor(cnn);
        }

        /// <summary>
        /// Configures the specified instance as a distributor.
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        async Task ConfigureDistributor(SqlConnection cnn)
        {
            await cnn.OpenAsync();
            cnn.ChangeDatabase("master");

            var distributorName = await cnn.GetServerPropertyAsync("SERVERNAME");

            // configure instance as distributor
            await cnn.ExecuteNonQueryAsync($@"
                EXEC sp_adddistributor
                    @distributor = {distributorName},
                    @password = {AdminPassword}");

            // add distribution database
            await cnn.ExecuteNonQueryAsync($@"
                EXEC sp_adddistributiondb
                    @database = {DatabaseName},
                    @security_mode = 1");

            // enable self as publisher
            var knownPublisherName = (string)await cnn.ExecuteScalarAsync($@"
                SELECT  name
                FROM    msdb.dbo.MSdistpublishers
                WHERE   name = {distributorName}");
            if (knownPublisherName == null)
                await cnn.ExecuteSpAddDistPublisherAsync(distributorName, DatabaseName, 1, "false", 0, "MSSQLSERVER");

            await UpdateDirectoryAcl(cnn);
        }

        /// <summary>
        /// Updates the ACLs of the distributor directory.
        /// </summary>
        /// <param name="cnn"></param>
        /// <returns></returns>
        async Task UpdateDirectoryAcl(SqlConnection cnn)
        {
            // fix permissions on replication directory
            var distributorInfo = await cnn.ExecuteSpHelpDistributorAsync();
            if (distributorInfo.Directory != null &&
                distributorInfo.Account != null)
            {
                // distributor directory?
                var d = new DirectoryInfo(distributorInfo.Directory);
                if (d.Exists == false)
                {
                    // path stored as local UNC, append default domain of distributor
                    var u = new Uri(d.FullName);
                    if (u.IsUnc &&
                        u.Host.Contains(".") == false)
                    {
                        var b = new UriBuilder(u);
                        if (await cnn.GetServerDomainName() is string domainName)
                            b.Host += "." + domainName;
                        d = new DirectoryInfo(b.Uri.LocalPath);
                    }

                    if (d.Exists == false)
                    {
                        return;
                    }
                }

                await Task.Run(() =>
                {
                    try
                    {
                    // grant permissions to directory to distributor agent account
                    var acl = d.GetAccessControl();
                        acl.AddAccessRule(new FileSystemAccessRule(
                            distributorInfo.Account,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.InheritOnly,
                            AccessControlType.Allow));
                        d.SetAccessControl(acl);
                    }
                    catch (Exception e)
                    {
                    //logger.Error(e, "Unable to update distribution directory ACLs.");
                }
                });
            }
        }

    }

}
