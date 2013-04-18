﻿//Author: Richard Bunt
using System;
using System.Collections.Generic;
using System.Text;
using MinimalHTTPServer;
using System.Threading;
using System.Reflection;

namespace Telemachus
{
    public class DataLinkResponsibility : IHTTPRequestResponsibility
    {
        #region Constants

        public const String PAGE_PREFIX = "/telemachus/datalink";
        public const char ARGUMENTS_START = '?';
        public const char ARGUMENTS_ASSIGN = '=';
        public const char ARGUMENTS_DELIMETER = '&';
        public const char ACCESS_DELIMITER = '.';

        #endregion

        #region Fields
        
        List<DataLinkHandler> APIHandlers = new List<DataLinkHandler>();
        DataSources dataSources = new DataSources();
        DataSourceResultFormatter resultFormatter = new JavaScriptGeneralFormatter();
        VesselChangeDetector vesselChangeDetector = new VesselChangeDetector();

        #endregion

        #region Data Rate Fields

        public const int RATE_AVERAGE_SAMPLE_SIZE = 20;
        public UpLinkDownLinkRate dataRates { get { return itsDataRates; } set { itsDataRates = value; } }
        private UpLinkDownLinkRate itsDataRates = new UpLinkDownLinkRate(RATE_AVERAGE_SAMPLE_SIZE);

        #endregion

        #region Initialisation

        public DataLinkResponsibility()
        {
            loadHandlers();
        }

        private void loadHandlers()
        {
            APIHandlers.Add(new PausedDataLinkHandler());
            APIHandlers.Add(new OrbitDataLinkHandler());
            APIHandlers.Add(new SensorDataLinkHandler(vesselChangeDetector));
            APIHandlers.Add(new VesselDataLinkHandler());
            APIHandlers.Add(new FlightDataLinkHandler());
            APIHandlers.Add(new BodyDataLinkHandler());
            APIHandlers.Add(new TimeWarpDataLinkHandler());
            APIHandlers.Add(new MechJebDataLinkHandler());
            APIHandlers.Add(new ResourceDataLinkHandler());
            APIHandlers.Add(new DefaultDataLinkHandler());
        }

        #endregion

        #region IHTTPRequestResponsibility

        public bool process(AsynchronousServer.ClientConnection cc, HTTPRequest request)
        {
            if (request.path.StartsWith(PAGE_PREFIX))
            {
                dataRates.addUpLinkPoint(System.DateTime.Now, request.path.Length);
                
                dataSources.vessel = FlightGlobals.ActiveVessel;
                vesselChangeDetector.update(FlightGlobals.ActiveVessel);

                String returnMessage = new OKPage(
                   argumentsParse(
                   request.path.Remove(
                   0, request.path.IndexOf(ARGUMENTS_START) + 1), dataSources)
                   ).ToString();
                cc.Send(returnMessage);

                dataRates.addDownLinkPoint(System.DateTime.Now, returnMessage.Length);

                return true;
            }

            return false;
        }

        #endregion

        #region API
        
        public void getAPIList(ref List<KeyValuePair<String, String>> APIList)
        {
            foreach (DataLinkHandler APIHandler in APIHandlers)
            {
                APIHandler.appendAPIList(ref APIList);
            }

        }
            
        #endregion

        #region Parse URL

        private String argumentsParse(String args, DataSources dataSources)
        {
            StringBuilder sb = new StringBuilder();
            String[] argsSplit = args.Split(ARGUMENTS_DELIMETER);

            foreach (String arg in argsSplit)
            {
                string refArg = arg;
                PluginLogger.Log("[Telemachus]" + refArg);
                parseParams(ref refArg, ref dataSources);
                sb.Append(argumentParse(refArg, dataSources));

                //Only parse the paused argument if the active vessel is null
                if (dataSources.vessel == null)
                {
                    break;
                }
            }

            return sb.ToString();
        }

        private void parseParams(ref String arg, ref DataSources dataSources)
        {
            dataSources.args.Clear();

            try
            {
                if (arg.Contains("["))
                {
                    
                    String[] argsSplit = arg.Split('[');
                    argsSplit[1] = argsSplit[1].Substring(0, argsSplit[1].Length - 1);
                    arg = argsSplit[0];
                    String[] paramSplit = argsSplit[1].Split(',');

                    for (int i = 0; i < paramSplit.Length; i++)
                    {
                        dataSources.args.Add(paramSplit[i]);
                    }
                }
            }
            catch (Exception e)
            {
                PluginLogger.Log("[Telemachus] " + e.Message + " " + e.StackTrace);
            }
        }

        private String argumentParse(String args, DataSources dataSources)
        {
            String[] argsSplit = args.Split(ARGUMENTS_ASSIGN);
            APIEntry result = null;

            foreach (DataLinkHandler APIHandler in APIHandlers)
            {
                if (APIHandler.process(argsSplit[1], out result))
                {
                    break;
                }
            }
            
            object r = result.function(dataSources);
            return resultFormatter.formatWithAssignment(
                resultFormatter.format(r, r.GetType()), argsSplit[0]);
        }

        #endregion
    }

    public class DataSources
    {
        #region Fields

        public Vessel vessel;
        public List<String> args = new List<String>();

        #endregion
    }
}
