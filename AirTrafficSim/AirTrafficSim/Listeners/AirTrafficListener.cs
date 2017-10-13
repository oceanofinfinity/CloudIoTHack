﻿using Newtonsoft.Json;
using ppatierno.AzureSBLite.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace AirTrafficSim.Listeners
{
    public class AirTrafficListener
    {
        private EventHubClient client { get; set; }
        private EventHubConsumerGroup consumerGroup { get; set; }
        private EventHubReceiver receiver { get; set; }

        public bool IsConfigured
        {
            get
            {
                return Common.CoreConstants.SharedEventHubEndpoint.ToLower().Contains("endpoint=sb://");
            }
        }

        public async void StartListeningAsync()
        {
            this.client = EventHubClient.CreateFromConnectionString(Common.CoreConstants.SharedEventHubEndpoint, Common.CoreConstants.SharedAirTrafficHubName);

            this.consumerGroup = this.client.GetDefaultConsumerGroup();
            this.receiver = this.consumerGroup.CreateReceiver("0", DateTime.Now.ToUniversalTime());

            await Task.Run(() => StartListeningForTrafficCommands());
        }

        private async void StartListeningForTrafficCommands()
        {
            List<PlaneStatusInformation> statusInfo = new List<PlaneStatusInformation>();

            while (true)
            {
                await Task.Delay(1);

                try
                {
                    var eventData = this.receiver.Receive();

                    if (eventData != null)
                    {
                        byte[] bytes = eventData.GetBytes();

                        var payload = Encoding.UTF8.GetString(bytes);

                        statusInfo.Clear();

                        try
                        {
                            foreach (var info in payload.Split("\r\n".ToCharArray(),
                                StringSplitOptions.RemoveEmptyEntries))
                            {
                                var status = JsonConvert.DeserializeObject<PlaneStatusInfo>(info);

                                statusInfo.Add(new PlaneStatusInformation()
                                {
                                    Plane1 = status.plane1,
                                    Plane2 = status.plane2,
                                    Distance = Convert.ToDouble(status.distance),

                                });
                            }
                        }
                        catch
                        {
                            
                        }
                    }

                    List<FilteredPlaneStatusInfo> filteredStatus = new List<FilteredPlaneStatusInfo>();

                    filteredStatus.AddRange(from op in statusInfo select new FilteredPlaneStatusInfo() { DisplayName = op.Plane1, Distance = op.Distance, });
                    filteredStatus.AddRange(from op in statusInfo select new FilteredPlaneStatusInfo() { DisplayName = op.Plane2, Distance = op.Distance, });

                    var orderedPlanes = filteredStatus.OrderBy(o => o.Distance).GroupBy(g => g.DisplayName);

                    var finalPlaneStatus = from plane in orderedPlanes
                                           select new
                                           {
                                               displayName = plane.Key,
                                               minimumDistance = plane.First().Distance,
                                           };

                    var atRiskPlanes = (from item in finalPlaneStatus
                                        where item.minimumDistance < Common.CoreConstants.AtRiskThreshold
                                        select item.displayName).Distinct().ToList();

                    App.ViewModel.AtRiskPlanes = atRiskPlanes;

                    App.ViewModel.UpdateAllActivePlaneStatus();
                }
                catch { }

            }
        }
    }
}
