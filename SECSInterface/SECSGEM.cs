﻿using log4net;
using QSACTIVEXLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransferControl.Engine;
using TransferControl.Management;

namespace SECSInterface
{
    public class SECSGEM : IHostInterfaceReport
    {
        static ILog logger = LogManager.GetLogger(typeof(SECSGEM));
        IUIReport _Report;
        SECSIni SECSSetting = new SECSIni();
        public QSACTIVEXLib.QSWrapper axQSWrapper1; //*new
        public QGACTIVEXLib.QGWrapper axQGWrapper1; //*new



        public SECSGEM(IUIReport Report)
        {
            Init();
            _Report = Report;
            // SECS Init
            axQGWrapper1 = new QGACTIVEXLib.QGWrapper(); //*new
            axQGWrapper1.QGEvent += new QGACTIVEXLib._IQGWrapperEvents_QGEventEventHandler(axQGWrapper1_QGEvent); //*new
            axQGWrapper1.PPEvent += new QGACTIVEXLib._IQGWrapperEvents_PPEventEventHandler(axQGWrapper1_PPEvent); //*new
            axQGWrapper1.TerminalMsgReceive += new QGACTIVEXLib._IQGWrapperEvents_TerminalMsgReceiveEventHandler(axQGWrapper1_TerminalMsgReceive); //*new


            axQSWrapper1 = new QSACTIVEXLib.QSWrapper(); //*new
            axQSWrapper1.QSEvent += new QSACTIVEXLib._IQSWrapperEvents_QSEventEventHandler(axQSWrapper1_QSEvent); //*new
            //' SECS-I Parameters
            axQSWrapper1.T1 = float.Parse(SECSSetting.SECSI.T1);
            axQSWrapper1.T2 = float.Parse(SECSSetting.SECSI.T2);
            axQSWrapper1.T4 = int.Parse(SECSSetting.SECSI.T4);
            axQSWrapper1.lBaudRate = int.Parse(SECSSetting.SECSI.BaudRate);
            axQSWrapper1.lCOMPort = int.Parse(SECSSetting.SECSI.ComPort);
            if (SECSSetting.SECSI.Role.Equals("Host"))
            {
                axQSWrapper1.SECS_Connect_Mode = SECS_COMM_MODE.SECS_HOST_MODE;
            }
            else
            {
                axQSWrapper1.SECS_Connect_Mode = SECS_COMM_MODE.SECS_EQUIP_MODE;
            }

            //' HSMS-SS Parameters
            axQSWrapper1.T5 = int.Parse(SECSSetting.HSMS.T5);
            axQSWrapper1.T6 = int.Parse(SECSSetting.HSMS.T6);
            axQSWrapper1.T7 = int.Parse(SECSSetting.HSMS.T7);
            axQSWrapper1.T8 = int.Parse(SECSSetting.HSMS.T8);
            axQSWrapper1.lLinkTestPeriod = int.Parse(SECSSetting.HSMS.LinkTestPeriod);
            axQSWrapper1.szLocalIP = SECSSetting.HSMS.LocalIp;
            axQSWrapper1.nLocalPort = int.Parse(SECSSetting.HSMS.LocalPort);
            axQSWrapper1.szRemoteIP = SECSSetting.HSMS.RemoteIp;
            axQSWrapper1.nRemotePort = int.Parse(SECSSetting.HSMS.RemotePort);
            if (SECSSetting.HSMS.Role.Equals("Active"))
            {
                axQSWrapper1.HSMS_Connect_Mode = HSMS_COMM_MODE.HSMS_ACTIVE_MODE;
            }
            else
            {
                axQSWrapper1.HSMS_Connect_Mode = HSMS_COMM_MODE.HSMS_PASSIVE_MODE;
            }
            //' Common Parameters
            axQSWrapper1.T3 = int.Parse(SECSSetting.HSMS.T3);
            axQSWrapper1.lDeviceID = int.Parse(SECSSetting.HSMS.DeviceID);
            if (SECSSetting.ConnectMode.Equals("HSMS"))
            {
                axQSWrapper1.lCOMM_Mode = COMMMODE.HSMS_MODE;
            }
            else
            {
                axQSWrapper1.lCOMM_Mode = COMMMODE.SECS_MODE;
            }

            axQSWrapper1.lLogEnable = 1;
            axQSWrapper1.lFlowControlEnable = 0;
            try
            {
                int state = axQSWrapper1.Initialize();

                string path = System.Environment.CurrentDirectory; //& "\.."
                state = axQGWrapper1.Initialize(path);

                object objVal = "EqMDLN";
                int lResult = axQGWrapper1.UpdateSV((int)GemSystemID.GEM_MDLN, ref objVal);

                objVal = "SofRev";
                lResult = axQGWrapper1.UpdateSV((int)GemSystemID.GEM_SOFTREV, ref objVal);

                // E40

                Process_E40_ProcessJobStateModel(PJSM_CMD_RESET);

                // E87
                object objTemp = (object)2; // LIST_2;
                int g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_STATE_INFO, ref objTemp);

                foreach (Node p in NodeManagement.GetLoadPortList())
                {
                    Process_E87_LPTSM(LPTSM_CMD_IN_SERVICE, p.Name);

                    Process_E87_AccessModeStateModel(AMSM_CMD_CHANGE_TO_MANUAL, p.Name);
                }

                // E94
                Process_E94_ControlJobStateModel(CJSM_CMD_RESET);


                objTemp = (object)g_iE94_QueueAvailableSpace;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_SV_QUEUE_AVAILABLE_SPACE, ref objTemp);

                //*********************************************************************************************

                state = axQGWrapper1.EnableComm();
                state = axQSWrapper1.Start();

            }
            catch (Exception ex)
            {

                logger.Error(ex.StackTrace);
            }

        }

        private void Process_E40_ProcessJobStateModel(int iCommand)
        {
            object sender;
            EventArgs e;
            e = new EventArgs();
            sender = new object();

            if (iCommand == PJSM_CMD_RESET)
            {
                PJ_Data1.PJState = PJSM_NOT_EXIST;
            }

            if (iCommand == PJSM_CMD_ACCEPT_PJ_CREATE)
            {
                PJ_Data1.PJState = PJSM_0_QUEUED;
                g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT1_QUEUED);
            }

            switch (PJ_Data1.PJState)
            {

                case PJSM_0_QUEUED:
                    if (iCommand == PJSM_CMD_SETTING_UP)
                    {
                        PJ_Data1.PJState = PJSM_1_SETTING_UP;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT2_SETTING_UP);
                        if (PJ_Data1.ProcessStart == PRPROCESSSTART_1_AUTOMATIC_START)
                        {
                            PJ_Data1.PJState = PJSM_3_PROCESSING;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT4_PROCESSING);
                            Process_E87_CSM_CarrierAccessingStatus(CSM_CMD_CARRIER_START_PROCESS);
                        }
                        else
                        {
                            PJ_Data1.PJState = PJSM_2_WAITING_FOR_START;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT3_WAITING_FOR_START);
                        }
                    }
                    if ((iCommand == PJSM_CMD_STOP) || (iCommand == PJSM_CMD_CANCEL) || (iCommand == PJSM_CMD_ABORT))
                    {
                        PJ_Data1.PJState = PJSM_NOT_EXIST;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT11_STOPPING);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    break;
                case PJSM_2_WAITING_FOR_START:
                    if (iCommand == PJSM_CMD_START)
                    {
                        PJ_Data1.PJState = PJSM_3_PROCESSING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT5_PROCESSING);
                        Process_E87_CSM_CarrierAccessingStatus(CSM_CMD_CARRIER_START_PROCESS);
                    }
                    if (iCommand == PJSM_CMD_STOP)
                    {
                        PJ_Data1.PJState = PJSM_8_STOPPING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT11_STOPPING);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    if (iCommand == PJSM_CMD_ABORT)
                    {
                        PJ_Data1.PJState = PJSM_9_ABORTING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT13_ABORTING);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT16_NO_STATE);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    break;
                case PJSM_3_PROCESSING:
                    if (iCommand == PJSM_CMD_PAUSE)
                    {
                        PJ_Data1.PJState = PJSM_6_PAUSING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT6_PROCESSCOMPLETE);
                        PJ_Data1.PJState = PJSM_7_PAUSED;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT9_PAUSED);
                    }
                    if (iCommand == PJSM_CMD_PROCESS_COMPLETE)
                    {
                        PJ_Data1.PJState = PJSM_4_PROCESS_COMPLETE;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT7_NO_STATE);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                    }
                    if (iCommand == PJSM_CMD_STOP)
                    {
                        PJ_Data1.PJState = PJSM_8_STOPPING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT11_STOPPING);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    if (iCommand == PJSM_CMD_ABORT)
                    {
                        PJ_Data1.PJState = PJSM_9_ABORTING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT13_ABORTING);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT16_NO_STATE);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    break;
                case PJSM_4_PROCESS_COMPLETE:
                    if (iCommand == PJSM_CMD_FOUP_REMOVED)
                    {
                        PJ_Data1.PJState = PJSM_NOT_EXIST;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT17_NO_STATE);
                    }
                    break;
                case PJSM_7_PAUSED:
                    if (iCommand == PJSM_CMD_RESUME)
                    {
                        PJ_Data1.PJState = PJSM_3_PROCESSING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT10_EXECUTING);
                    }
                    if (iCommand == PJSM_CMD_STOP)
                    {
                        PJ_Data1.PJState = PJSM_8_STOPPING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT12_STOPPING);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    if (iCommand == PJSM_CMD_ABORT)
                    {
                        PJ_Data1.PJState = PJSM_9_ABORTING;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT15_ABORTING);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT16_NO_STATE);
                        Process_E94_ControlJobStateModel(CJSM_CMD_PJ_COMPLETE);
                        Process_E87_AccessModeStateModel(CSM_CMD_CARRIER_PROCESS_STOPPED);
                        btn_E87_ReadyToUnload_Click(sender, e);
                    }
                    break;
                case PJSM_8_STOPPING:
                    if (iCommand == PJSM_CMD_FOUP_REMOVED)
                    {
                        PJ_Data1.PJState = PJSM_NOT_EXIST;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT17_NO_STATE);
                    }
                    break;
                case PJSM_9_ABORTING:
                    if (iCommand == PJSM_CMD_FOUP_REMOVED)
                    {
                        PJ_Data1.PJState = PJSM_NOT_EXIST;
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E40_CE_PJSM_SCT16_NO_STATE);
                    }
                    break;
            }

        }

        private int PortNameConvert(string PortName)
        {
            int result = 0;
            try
            {
                string tmp = PortName.Substring(PortName.Length - 2);
                result = int.Parse(tmp);
            }
            catch (Exception e)
            {
                logger.Error(e.StackTrace);
            }
            return result;
        }

        private void E87_InService(string PortName)
        {
            g_iE87LPTSM_LPID = PortNameConvert(PortName);
            Process_E87_LPTSM(LPTSM_CMD_IN_SERVICE);
        }

        private void E87_OutOfService(string PortName)
        {
            g_iE87LPTSM_LPID = PortNameConvert(PortName);
            Process_E87_LPTSM(LPTSM_CMD_OUT_SERVICE);
        }

        private void E87_LoadComplete(string PortName)
        {
            Node p = NodeManagement.Get(PortName);
            if (p == null)
            {
                logger.Error("E87_LoadComplete err: Name " + PortName + " not found!");
                return;
            }
            g_iE87LPTSM_LPID = PortNameConvert(PortName);
            Process_E87_LPTSM(LPTSM_CMD_LOAD_COMPLETE);
            Process_E87_CSM_CarrierIDStatus(CSM_CMD_CARRIER_ID_READ_SUCCESS);
            Process_E87_CSM_CarrierSlopMapStatus(CSM_CMD_CARRIER_IS_INSTANTIATED);
            Process_E87_CSM_CarrierAccessingStatus(CSM_CMD_CARRIER_IS_INSTANTIATED);

        }

        private void E87_UnLoadComplete(string PortName)
        {
            g_iE87LPTSM_LPID = PortNameConvert(PortName);
            Process_E87_LPTSM(LPTSM_CMD_UNLOAD_COMPLETE);
            Process_E87_CSM_CarrierIDStatus(CSM_CMD_UNLOAD_COMPLETE);
            Process_E40_ProcessJobStateModel(PJSM_CMD_FOUP_REMOVED);
            Process_E94_ControlJobStateModel(CJSM_CMD_CJ_DELETE);
            g_iE40PJDataNumber = 0;
        }

        private void E87_ReadyToUnload(string PortName)
        {
            g_iE87LPTSM_LPID = PortNameConvert(PortName);
            Process_E87_LPTSM(LPTSM_CMD_READY_TO_UNLOAD);
        }

        private void Process_E87_CSM_CarrierSlopMapStatus(int iCommand,string portName)
        {
            int i, j;
            //long lSVID, lRetval;
            object objTemp;
            //QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            //object Value;
            int iSlopMapValue;

            if ((iCommand == CSM_CMD_UNLOAD_COMPLETE) || (iCommand == CSM_CMD_CARRIER_IS_INSTANTIATED))
            {
                Carrier_Data1.SlopMapStatus = CARRIER_SLOP_MAP_STATUS_0_SLOP_MAP_NOT_READ;
            }

            switch (Carrier_Data1.SlopMapStatus)
            {
                case CARRIER_SLOP_MAP_STATUS_0_SLOP_MAP_NOT_READ:
                    if (iCommand == CSM_CMD_SLOP_MAP_READ_SUCCESS)
                    {
                        Carrier_Data1.SlopMapStatus = CARRIER_SLOP_MAP_STATUS_1_WAITING_FOR_HOST;

                        // Update  SV  
                        objTemp = (object)Carrier_Data1.SlopMapStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_STATUS_1, ref objTemp);

                        objTemp = (object)Carrier_Data1.SlopMapCapcity;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_1, ref objTemp);
                        for (i = 0; i < Carrier_Data1.SlopMapCapcity; i++)
                        {
                            iSlopMapValue = CARRIER_SLOP_MAP_3_CORRECTLY_OCCUPIED;
                            objTemp = (object)iSlopMapValue;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                        }

                        // Update  DV
                        objTemp = (object)Carrier_Data1.SlopMapCapcity;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP, ref objTemp);
                        for (i = 0; i < Carrier_Data1.SlopMapCapcity; i++)
                        {
                            iSlopMapValue = CARRIER_SLOP_MAP_3_CORRECTLY_OCCUPIED;
                            objTemp = (object)iSlopMapValue;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);   // for SlotMap DV 
                        }

                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        objTemp = (object)Carrier_Data1.SlopMapStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT14_WAITING_FOR_HOST);
                    }
                    break;
                case CARRIER_SLOP_MAP_STATUS_1_WAITING_FOR_HOST:
                    if (iCommand == CSM_CMD_PROCEED_WITH_CARRIER_RECEIVED)
                    {
                        Carrier_Data1.SlopMapStatus = CARRIER_SLOP_MAP_STATUS_2_SLOT_MAP_VERIFICATION_OK;

                        // Update SV
                        objTemp = (object)Carrier_Data1.SlopMapStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_STATUS_1, ref objTemp);

                        // Update DV
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        objTemp = (object)Carrier_Data1.SlopMapStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT15_SLOT_MAP_VERIFICATION_OK_HOST);
                        // DV port ID ,Location ID need update
                    }

                    break;
            }

        }

        // LP1 Carrier ID Status
        private void Process_E87_CSM_CarrierIDStatus(int iCarrierIDStatus_Command,string portName)
        {
            long lSVID, lRetval;
            object objTemp;
            QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            object Value;

            if (iCarrierIDStatus_Command == CSM_CMD_UNLOAD_COMPLETE)
            {
                objTemp = (object)Carrier_Data1.CarrierID;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT21_NO_STATE);

                // Clear Carrier ID and status
                Carrier_Data1.IDStatus = CARRIER_ID_STATUS_0_ID_NOT_READ;
                Carrier_Data1.CarrierID = "";
                objTemp = (object)Carrier_Data1.CarrierID;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CARRIER_ID_1, ref objTemp);
            }

            switch (Carrier_Data1.IDStatus)
            {
                case CARRIER_ID_STATUS_0_ID_NOT_READ:
                    if (iCarrierIDStatus_Command == CSM_CMD_CARRIER_ID_READ_SUCCESS)
                    {
                        // Update SV
                        Carrier_Data1.IDStatus = CARRIER_ID_STATUS_1_WAITING_FOR_HOST;
                        Process_E87_CSM_CarrierObjCreate();

                        // Update DV
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        objTemp = (object)Carrier_Data1.IDStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT3_WAITING_FOR_HOST);
                    }
                    break;
                case CARRIER_ID_STATUS_1_WAITING_FOR_HOST:
                    if (iCarrierIDStatus_Command == CSM_CMD_PROCEED_WITH_CARRIER_RECEIVED)
                    {
                        Carrier_Data1.IDStatus = CARRIER_ID_STATUS_2_ID_VERIFICATION_OK;

                        // Update SV

                        objTemp = (object)Carrier_Data1.IDStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CARRIER_ID_STATUS_1, ref objTemp);

                        // Update DV
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        objTemp = (object)Carrier_Data1.IDStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT8_ID_VERIFICATION_OK);
                    }
                    if (iCarrierIDStatus_Command == CSM_CMD_CANCEL_CARRIER_AT_PORT_RECEIVED)
                    {
                        Carrier_Data1.IDStatus = CARRIER_ID_STATUS_3_ID_VERIFICATION_FAILED;

                        // Update SV
                        objTemp = (object)Carrier_Data1.IDStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CARRIER_ID_STATUS_1, ref objTemp);

                        // Update DV
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        objTemp = (object)Carrier_Data1.IDStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT9_ID_VERIFICATION_FAIL);
                    }
                    break;
            }


        }

        private void Process_E87_CSM_CarrierObjCreate()
        {
            object objTemp;
            int i;
            int iSlopMapValue;
            //Carrier_Data1.CarrierID = sCarrierID;
            //Carrier_Data1.Capcity   = CarrierCommand1.Capcity;
            Carrier_Data1.ContentMapList = 0;
            Carrier_Data1.LocationID = "LP1";
            //Carrier_Data1.SlopMapStatus = CARRIER_SLOP_MAP_STATUS_0_SLOP_MAP_NOT_READ;
            Carrier_Data1.SubstrateCount = 0;
            Carrier_Data1.Usage = "";

            objTemp = (object)Carrier_Data1.CarrierID;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CARRIER_ID_1, ref objTemp);

            objTemp = (object)Carrier_Data1.Capcity;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CAPACITY_1, ref objTemp);

            objTemp = (object)Carrier_Data1.IDStatus;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CARRIER_ID_STATUS_1, ref objTemp);

            objTemp = (object)Carrier_Data1.ContentMapList;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CONTENT_MAP_1, ref objTemp);

            objTemp = (object)Carrier_Data1.LocationID;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LOCATION_ID_1, ref objTemp);

            objTemp = (object)Carrier_Data1.SlopMapCapcity;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_1, ref objTemp);

            objTemp = (object)Carrier_Data1.SlopMapStatus;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_STATUS_1, ref objTemp);

            objTemp = (object)Carrier_Data1.SubstrateCount;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SUBSTRATE_COUNT_1, ref objTemp);

            objTemp = (object)Carrier_Data1.Usage;
            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_USAGE_1, ref objTemp);


            for (i = 0; i < Carrier_Data1.SlopMapCapcity; i++)
            {
                iSlopMapValue = CARRIER_SLOP_MAP_0_UNDEFINED;
                objTemp = (object)iSlopMapValue;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
            }

        }

        // * E87 LPTSM
        private void Process_E87_LPTSM(int iE87LPTSM_Command,string portName)
        {
            long lSVID, lRetval;
            object objTemp;
            QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            object Value;
            int intTemp;

            if ((g_iE87LPTSM_LPTransferState1 != LPTSM_0_OUT_OF_SERVICE) &&
                (iE87LPTSM_Command == LPTSM_CMD_OUT_SERVICE))
            {
                g_iE87LPTSM_LPTransferState1 = LPTSM_0_OUT_OF_SERVICE;

                objTemp = (object)g_iE87LPTSM_LPTransferState1;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                objTemp = (object)g_iE87LPTSM_LPID;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT3_OUT_OF_SERVICE);
                iE87LPTSM_Command = 0;
            }
            switch (g_iE87LPTSM_LPTransferState1)
            {
                case LPTSM_0_OUT_OF_SERVICE:
                    if (iE87LPTSM_Command == LPTSM_CMD_IN_SERVICE)
                    {
                        g_iE87LPTSM_LPTransferState1 = LPTSM_2_READY_TO_LOAD;  //assume LP is empty/, machine enters "Ready To Load"

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT2_IN_SERVICE);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT5_READY_LOAD);


                    }
                    iE87LPTSM_Command = 0;
                    break;
                case LPTSM_1_TRANSFER_BLOCKED:
                    if (iE87LPTSM_Command == LPTSM_CMD_READY_TO_UNLOAD)
                    {
                        g_iE87LPTSM_LPTransferState1 = LPTSM_3_READY_TO_UNLOAD;

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT9_READY_TO_UNLOAD);
                    }
                    if (iE87LPTSM_Command == LPTSM_CMD_READY_TO_LOAD)
                    {
                        g_iE87LPTSM_LPTransferState1 = LPTSM_2_READY_TO_LOAD;

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT5_READY_LOAD);
                    }
                    iE87LPTSM_Command = 0;
                    break;
                case LPTSM_2_READY_TO_LOAD:
                    if (iE87LPTSM_Command == LPTSM_CMD_LOAD_COMPLETE)
                    {
                        g_iE87LPTSM_LPTransferState1 = LPTSM_1_TRANSFER_BLOCKED;

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT6_TRANSFER_BLOCKED);

                    }
                    iE87LPTSM_Command = 0;
                    break;
                case LPTSM_3_READY_TO_UNLOAD:
                    if (iE87LPTSM_Command == LPTSM_CMD_UNLOAD_COMPLETE)
                    {
                        //to transfer block
                        g_iE87LPTSM_LPTransferState1 = LPTSM_1_TRANSFER_BLOCKED;

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT6_TRANSFER_BLOCKED);
                        iE87LPTSM_Command = LPTSM_CMD_READY_TO_LOAD; // ?? for access mode:Manual

                        // to ready to load
                        g_iE87LPTSM_LPTransferState1 = LPTSM_2_READY_TO_LOAD;

                        objTemp = (object)g_iE87LPTSM_LPTransferState1;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_LP1_TRANSFER_STATE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_TARANSFER_STATE, ref objTemp);
                        objTemp = (object)g_iE87LPTSM_LPID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_LPTSM_SCT8_READY_TO_LOAD);
                    }
                    break;
            }


            lSVID = EqpID.E87_SV_LP1_TRANSFER_STATE;
            axQGWrapper1.GetSV((int)lSVID, out GetFormat, out Value);
            intTemp = int.Parse(Value.ToString());
            g_iE87LPTSM_LPTransferState1 = intTemp;


        }

        // LP1 Access Mode
        private void Process_E87_AccessModeStateModel(int iCommand,string portName)
        {
            //long lSVID, lRetval;
            object objTemp;
            //QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            //object Value;
            int iPortID;


            iPortID = 1;
            switch (g_iE87AMSM_AccessMode)
            {
                case AMSM_0_MANUAL_MODE:
                    if ((iCommand == AMSM_CMD_CHANGE_TO_AUTO) || (iCommand == AMSM_CMD_CHANGE_TO_AUTO_ALL))
                    {
                        g_iE87AMSM_AccessMode = AMSM_1_AUTO_MODE;

                        objTemp = (object)iPortID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        objTemp = (object)g_iE87AMSM_AccessMode;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_ACCESS_MODE, ref objTemp);

                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_AMSM_SCT2_TO_AUTO);

                    }
                    break;
                case AMSM_1_AUTO_MODE:
                    if ((iCommand == AMSM_CMD_CHANGE_TO_MANUAL) || (iCommand == AMSM_CMD_CHANGE_TO_MANUAL_ALL))
                    {
                        g_iE87AMSM_AccessMode = AMSM_0_MANUAL_MODE;
                        objTemp = (object)iPortID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                        objTemp = (object)g_iE87AMSM_AccessMode;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_ACCESS_MODE, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_AMSM_SCT3_TO_MANUAL);

                    }
                    break;

            }



        }

        private void btn_E87_ReadyToUnload_Click(object sender, EventArgs e)
        {
            g_iE87LPTSM_LPID = LPTSM_PORT_ID_1;
            Process_E87_LPTSM(LPTSM_CMD_READY_TO_UNLOAD);
        }

        private void Process_E87_CSM_CarrierAccessingStatus(int iCommand,string portName)
        {
            int i, j;
            long lSVID, lRetval;
            object objTemp;
            QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            object Value;
            int iSlopMapValue;

            if ((iCommand == CSM_CMD_UNLOAD_COMPLETE) || (iCommand == CSM_CMD_CARRIER_IS_INSTANTIATED))
            {
                Carrier_Data1.AccessingStatus = CARRIER_ACCESSING_STATUS_0_NOT_ACCESSED;
            }

            switch (Carrier_Data1.AccessingStatus)
            {
                case CARRIER_ACCESSING_STATUS_0_NOT_ACCESSED:
                    if (iCommand == CSM_CMD_CARRIER_START_PROCESS)
                    {
                        //
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        Carrier_Data1.AccessingStatus = CARRIER_ACCESSING_STATUS_1_IN_ACCESS;
                        objTemp = (object)Carrier_Data1.AccessingStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ACCESSING_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT18_IN_ACCESS);
                    }
                    break;
                case CARRIER_ACCESSING_STATUS_1_IN_ACCESS:
                    if (iCommand == CSM_CMD_CARRIER_PROCESS_COMPLETE)
                    {
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        Carrier_Data1.AccessingStatus = CARRIER_ACCESSING_STATUS_2_CARRIER_COMPLETE;
                        objTemp = (object)Carrier_Data1.AccessingStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ACCESSING_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT19_CARRIER_COMPLETE);
                    }
                    if (iCommand == CSM_CMD_CARRIER_PROCESS_STOPPED)
                    {
                        objTemp = (object)Carrier_Data1.CarrierID;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                        Carrier_Data1.AccessingStatus = CARRIER_ACCESSING_STATUS_3_CARRIER_STOPPED;
                        objTemp = (object)Carrier_Data1.AccessingStatus;
                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ACCESSING_STATUS, ref objTemp);
                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT20_CARRIER_STOPPED);
                    }

                    break;
            }
            //lSVID = GemSystemID.E87_SV_CARRIER_ID_1;
            //axQGWrapper1.GetSV((int)lSVID, out GetFormat, out Value);
            //lbl_LP1_CarrierID.Text = Value.ToString();



        }

        private void Process_E94_ControlJobStateModel(int iCommand)
        {
            //int i, j;
            //long lSVID, lRetval;
            object objTemp;
            //QGACTIVEXLib.SV_DATA_TYPE GetFormat;
            //object Value;
            //int iSlopMapValue;

            if (iCommand == CJSM_CMD_RESET)
            {
                CJ_Data1.CJState = CJSM_NOT_EXIST;
            }

            if (iCommand == CJSM_CMD_ACCEPT_CJ_CREATE)
            {

                g_iE94_QueueAvailableSpace = 0;
                objTemp = (object)g_iE94_QueueAvailableSpace;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_SV_QUEUE_AVAILABLE_SPACE, ref objTemp);

                CJ_Data1.CJState = CJSM_0_QUEUED;
                objTemp = (object)CJ_Data1.CJ_ObjID;
                g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);
                g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT1_QUEUED);

                CJ_Data1.CJState = CJSM_1_SELECTED;
                g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT3_SELECTED);

                if (CJ_Data1.StartMethod == START_METHOD_1_AUTO)
                {
                    CJ_Data1.CJState = CJSM_3_EXECUTING;
                    g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT5_EXECUTING);
                    Process_E40_ProcessJobStateModel(PJSM_CMD_SETTING_UP);
                }
                else
                {
                    CJ_Data1.CJState = CJSM_2_WAITING_FOR_START;
                    g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT6_WAITING_FOR_START);
                }

            }
            else
            {
                switch (CJ_Data1.CJState)
                {

                    case CJSM_2_WAITING_FOR_START:
                        if (iCommand == CJ_Command_1_CJStart)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_3_EXECUTING;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT7_EXECUTING);
                            Process_E40_ProcessJobStateModel(PJSM_CMD_SETTING_UP);
                        }
                        break;
                    case CJSM_3_EXECUTING:
                        if (iCommand == CJSM_CMD_PJ_COMPLETE)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_5_COMPLETED;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT10_COMPLETED);
                        }
                        if (iCommand == CJ_Command_2_CJPause)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_4_PAUSED;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT8_PAUSED);
                        }
                        break;
                        if (iCommand == CJ_Command_6_CJStop)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_5_COMPLETED;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT11_COMPLETED);
                        }
                        if (iCommand == CJ_Command_7_CJAbort)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_5_COMPLETED;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT12_COMPLETED);
                        }
                        break;
                    case CJSM_4_PAUSED:
                        if (iCommand == CJ_Command_3_CJResume)
                        {
                            objTemp = (object)CJ_Data1.CJ_ObjID;
                            g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E94_DV_CONTROL_JOB_ID, ref objTemp);

                            CJ_Data1.CJState = CJSM_3_EXECUTING;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT9_EXECUTING);
                        }
                        break;
                    case CJSM_5_COMPLETED:
                        if (iCommand == CJSM_CMD_CJ_DELETE)
                        {
                            Carrier_Data1.AccessingStatus = CARRIER_ACCESSING_STATUS_2_CARRIER_COMPLETE;
                            CJ_Data1.CJState = CJSM_NOT_EXIST;
                            g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E94_CE_CJSM_SCT13_NO_STATE);
                            // DV port ID ,Location ID need update
                        }

                        break;
                }
            }

        }

        public bool OnlieReq()
        {
            string path;
            long lResult;
            object objVal;
            //int iLength;
            //long lSVID;
            //byte bCOMM_Mode;
            //long lOperationResult;

            path = System.Environment.CurrentDirectory; //& "\.."
            int lQGInitResult = axQGWrapper1.Initialize(path);

            //lbl_ErrorCode.Text = lQGInitResult.ToString();
            if (lQGInitResult == 0)
            {
                //lbl_ErrorCode.BackColor = Color.GreenYellow;
                //AppendText("QuickGEM Initiation success." + "\r\n");
            }
            else
            {
                //lbl_ErrorCode.BackColor = Color.Red;
                //AppendText("QuickGEM Initiation error." + "\r\n");
                //MessageBox.Show("QuickGEM Initiation error. Press any key to close program !");
                //lQGInitResult = -2; //initial failed
                //Environment.Exit(0);
                return false;
            }

            objVal = "EqMDLN";
            lResult = axQGWrapper1.UpdateSV((int)GemSystemID.GEM_MDLN, ref objVal);

            objVal = "SofRev";
            lResult = axQGWrapper1.UpdateSV((int)GemSystemID.GEM_SOFTREV, ref objVal);



            return true;
        }



        public void OffLine()
        {
            int state = axQGWrapper1.DisableComm();
            state = axQSWrapper1.Stop();
        }

        private void Init()
        {
            string strFile = "Config/SECSConfig.ini";

            if (File.Exists(strFile))
            {
                TIniFile iniFile = new TIniFile(strFile);

                SECSSetting.HSMS.T3 = iniFile.ReadString("HSMS", "T3", "");
                SECSSetting.HSMS.T5 = iniFile.ReadString("HSMS", "T5", "");
                SECSSetting.HSMS.T6 = iniFile.ReadString("HSMS", "T6", "");
                SECSSetting.HSMS.T7 = iniFile.ReadString("HSMS", "T7", "");
                SECSSetting.HSMS.T8 = iniFile.ReadString("HSMS", "T8", "");
                SECSSetting.HSMS.LocalIp = iniFile.ReadString("HSMS", "LocalIp", "");
                SECSSetting.HSMS.LocalPort = iniFile.ReadString("HSMS", "LocalPort", "");
                SECSSetting.HSMS.RemoteIp = iniFile.ReadString("HSMS", "RemoteIp", "");
                SECSSetting.HSMS.RemotePort = iniFile.ReadString("HSMS", "RemotePort", "");
                SECSSetting.HSMS.LinkTestPeriod = iniFile.ReadString("HSMS", "LinkTestPeriod", "");
                SECSSetting.HSMS.Role = iniFile.ReadString("HSMS", "Role", "");
                SECSSetting.HSMS.DeviceID = iniFile.ReadString("HSMS", "DeviceID", "");

                SECSSetting.SECSI.T1 = iniFile.ReadString("SECS-I", "T1", "");
                SECSSetting.SECSI.T2 = iniFile.ReadString("SECS-I", "T2", "");
                SECSSetting.SECSI.T3 = iniFile.ReadString("SECS-I", "T3", "");
                SECSSetting.SECSI.T4 = iniFile.ReadString("SECS-I", "T4", "");
                SECSSetting.SECSI.Rty = iniFile.ReadString("SECS-I", "Rty", "");
                SECSSetting.SECSI.BaudRate = iniFile.ReadString("SECS-I", "BaudRate", "");
                SECSSetting.SECSI.ComPort = iniFile.ReadString("SECS-I", "ComPort", "");
                SECSSetting.SECSI.Role = iniFile.ReadString("SECS-I", "Role", "");
                SECSSetting.SECSI.DeviceID = iniFile.ReadString("SECS-I", "DeviceID", "");

                SECSSetting.ConnectMode = iniFile.ReadString("ConnectMode", "ConnectMode", "");
                iniFile.Close();
            }
        }

        private void axQGWrapper1_QGEvent(int lID, int S, int F, int W_Bit, int SystemBytes, object RawData, int Length) //*new
        {
            //ShowSECSIIMessage RawData
            axQSWrapper1.SendSECSIIMessage(S, F, W_Bit, ref SystemBytes, RawData);
            //AppendText("QuickGEM Send Request ===>" + "\r\n");
        }

        //******************************************************************************************
        //* QuickSECS Event Call Back Area
        //******************************************************************************************
        //private void axQSWrapper1_QSEvent(object sender, AxQSACTIVEXLib._IQSWrapperEvents_QSEventEvent e) //*old
        private void axQSWrapper1_QSEvent(int lID, EVENT_ID lMsgID, int S, int F, int W_Bit, int ulSystemBytes, object RawData, object Head, string pEventText) //*new
        {
            QGACTIVEXLib.PROCESS_MSG_RESULT lResult = axQGWrapper1.ProcessMessage((int)lMsgID, S, F, W_Bit, ulSystemBytes, ref RawData, ref Head, pEventText);
        }

        //******************************************************************************************
        //* QuickGEM Event Call Back Area
        //******************************************************************************************
        //private void axQGWrapper1_PPEvent(object sender, AxQGACTIVEXLib._IQGWrapperEvents_PPEventEvent e) //*old
        private void axQGWrapper1_PPEvent(QGACTIVEXLib.PP_TYPE MsgID, string PPID)
        {
            long lResult;
            object Value;
            switch (MsgID)
            {
                case QGACTIVEXLib.PP_TYPE.RECEIVE_E87_PROCEED_WITH_CARRIER://HOST驗證成功，開始MAPPING FOUP
                    
                    string[] split = PPID.Split(new Char[] { ',', '\t' });
                    string CSTID = split[0].ToString();
                    int portNo = int.Parse(split[1].ToString());
                    string portName = "LOADPORT"+ portNo.ToString("00");
                    Node port = NodeManagement.Get(portName);
                    if (port != null)
                    {
                        port.FoupIdStatus = CARRIER_ID_STATUS_2_ID_VERIFICATION_OK;

                        Work(portName, "LOADPORT_OPEN");
                    }
                    break;
                case QGACTIVEXLib.PP_TYPE.RECEIVE_E87_PROCEED_WITH_CARRIER_CONTENT_MAP:
                    lResult = axQGWrapper1.GetSV(GemSystemID.E87_CONTENT_MAP, out GetFormat, out Value);
                    lbl_ContentMap.Text = Value.ToString();

                    j = int.Parse(Value.ToString());
                    for (i = 0; i < j; i++)
                    {
                        lResult = axQGWrapper1.GetSV(GemSystemID.E87_CTMAP_LOTID1 + i, out GetFormat, out Value);
                        CarrierCommand1.LotID[i] = Value.ToString();
                        lResult = axQGWrapper1.GetSV(GemSystemID.E87_CTMAP_SUBSTID1 + i, out GetFormat, out Value);
                        CarrierCommand1.SubStID[i] = Value.ToString();
                    }
                    CarrierCommand1.ContentMapExistFlag = 1;
                    break;
            }
        }

        private void axQGWrapper1_TerminalMsgReceive(string Message)//*new
        {

        }


        public void On_Event_Trigger(string Type, string Source, string Name, string Value)
        {

        }

        public void On_TaskJob_Aborted(TaskJobManagment.CurrentProceedTask Task, string Location, string ReportType, string Message)
        {

        }

        public void On_TaskJob_Ack(TaskJobManagment.CurrentProceedTask Task)
        {

        }

        public void On_TaskJob_Finished(TaskJobManagment.CurrentProceedTask Task)
        {
            string TargetName = "";
            object objTemp;
            if (Task.Params.TryGetValue("Target", out TargetName))
            {
                Node Target = NodeManagement.Get(TargetName);
                if (Target != null)
                {
                    switch (Target.Type.ToUpper())
                    {
                        case "LOADPORT":
                            if (Target.AccessAutoMode)
                            {
                                switch (Task.ProceedTask.TaskName)
                                {
                                    case "LOADPORT_CLAMP":
                                        //發送CLAMP 事件
                                        objTemp = (object)PortNameConvert(Target.Name);
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                                        objTemp = (object)true;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CLAMP_STATE, ref objTemp);

                                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CLAMPED);

                                        //讀FOUP ID
                                        //先BYPASS
                                        //發送Read success
                                        Target.FoupID = "CSTID_01";
                                        objTemp = (object)PortNameConvert(Target.Name);
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);

                                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_ID_READ_SUCCESS);
                                        //發送LoadComplete
                                        E87_LoadComplete(Target.Name);
                                        break;
                                    case "LOADPORT_UNCLAMP":
                                        //發送UNCLAMP 事件
                                        objTemp = (object)PortNameConvert(Target.Name);
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);
                                        objTemp = (object)false;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CLAMP_STATE, ref objTemp);

                                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_UNCLAMPED);
                                        break;
                                    case "LOADPORT_OPEN":
                                        //發送SlotMapAvailable事件
                                        int count = 0;
                                        for (int i = 0; i < Target.MappingResult.Length; i++)
                                        {                                            
                                            switch (Target.MappingResult[i])
                                            {
                                                case '0':                                                    
                                                     objTemp = (object)CARRIER_SLOP_MAP_1_EMPTY;
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);      // for SlotMap DV 
                                                    break;
                                                case '1':
                                                    objTemp = (object)CARRIER_SLOP_MAP_3_CORRECTLY_OCCUPIED;
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);      // for SlotMap DV 
                                                    count++;
                                                    break;
                                                case '2':
                                                case 'E':
                                                    objTemp = (object)CARRIER_SLOP_MAP_5_CROSS_SLOTTED;
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);      // for SlotMap DV 
                                                    break;
                                                default:
                                                case '?':
                                                    objTemp = (object)CARRIER_SLOP_MAP_0_UNDEFINED;
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);      // for SlotMap DV 
                                                    break;
                                                case 'W':
                                                    objTemp = (object)CARRIER_SLOP_MAP_4_DOUBLE_SLOTTED;
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_IDATA_SLOT_MAP_1_1 + i, ref objTemp); // for SlotMap1 SV 
                                                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_1 + i, ref objTemp);      // for SlotMap DV 
                                                    break;
                                            }

                                            
                                        }

                                        // Update  SV  
                                        objTemp = (object)CARRIER_SLOP_MAP_STATUS_1_WAITING_FOR_HOST;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_SLOT_MAP_STATUS_1, ref objTemp);

                                        objTemp = (object)25;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_SV_CAPACITY_1, ref objTemp);

                                        // Update  DV
                                        objTemp = (object)25;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP, ref objTemp);

                                        objTemp = (object)PortNameConvert(Target.Name);
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);

                                        objTemp = (object)Target.FoupID;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_CARRIER_ID, ref objTemp);
                                        objTemp = (object)Carrier_Data1.SlopMapStatus;
                                        g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_SLOT_MAP_STATUS, ref objTemp);
                                        g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_CSM_SCT14_WAITING_FOR_HOST);

                                        break;
                                }
                            }
                            break;
                    }
                }
                else
                {
                    logger.Error("On_TaskJob_Finished err: Target " + TargetName + " not in Node list.");
                }
            }
            else
            {
                logger.Error("On_TaskJob_Finished err: Target not in Param.");
            }

        }

        public void On_Foup_Presence(string PortName, bool Presence)
        {
            object objTemp;
            Node port = NodeManagement.Get(PortName);
            if (port != null)
            {
                if (port.AccessAutoMode)
                {
                    //發送FOUP Arrived 事件
                    objTemp = (object)PortNameConvert(PortName);
                    g_lOperationResult = axQGWrapper1.UpdateSV((int)EqpID.E87_DV_PORT_ID, ref objTemp);                   

                    g_lOperationResult = axQGWrapper1.EventReportSend(EqpID.E87_CE_LP1_FOUP_ARRIVED);
                    Work(PortName, "LOADPORT_CLAMP");
                }
            }
            else
            {
                logger.Error(PortName + " not exist!");
            }
        }

        private void Work(string NodeName, string TaskName)
        {
            string Message = "";
            Dictionary<string, string> param = new Dictionary<string, string>();
            param.Add("@Target", NodeName);
            TaskJobManagment.CurrentProceedTask Task;
            RouteControl.Instance.TaskJob.Excute(GetId(), out Message, out Task, TaskName, param);
        }

        private string GetId()
        {
            return Guid.NewGuid().ToString();
        }

        struct SECSIni
        {
            public secsi SECSI;
            public hsms HSMS;
            public string ConnectMode;
        }

        struct secsi
        {
            public string T1;
            public string T2;
            public string T3;
            public string T4;
            public string BaudRate;
            public string ComPort;
            public string Rty;
            public string Role;
            public string DeviceID;
        }

        struct hsms
        {
            public string T3;
            public string T5;
            public string T6;
            public string T7;
            public string T8;
            public string LinkTestPeriod;
            public string LocalIp;
            public string LocalPort;
            public string RemoteIp;
            public string RemotePort;
            public string Role;
            public string DeviceID;
        }

        long g_lOperationResult;
        public int g_iE94_QueueAvailableSpace = 0;
        public int g_iE87LPTSM_LPTransferState1 = 0;
        public int g_iE87AMSM_AccessMode = 0;
        public int[] SlotID;
        public int g_iE87LPTSM_LPID;
        public int g_iE40PJDequeue_PJID_Number = 0;
        public int g_iE40PJDataNumber = 0;

        // E87 data -----------------------------------------
       

        public struct CarrierCommand
        {
            //Command Data
            public int Command;
            public string CarrierID;
            public int PTN;
            public int Capcity;
            public int ContentMapExistFlag;
            public int SlotMapExistFlag;
            public string[] LotID;
            public string[] SubStID;
            public int[] SlopMap;

            public CarrierCommand(int p1)
            {
                Command = 0;
                CarrierID = "";
                PTN = 0;
                Capcity = 0;
                ContentMapExistFlag = 0;
                SlotMapExistFlag = 0;

                LotID = new string[0];
                SubStID = new string[0];
                SlopMap = new int[0];
            }
        }

        CarrierCommand CarrierCommand1 = new CarrierCommand();

        public struct ChgAccess_Data
        {
            //Receive Data
            public int ChangeAccessCommand;
            public int PTN_Number;
            public int[] PTN;

            public ChgAccess_Data(int p1)
            {
                ChangeAccessCommand = 0;
                PTN_Number = 0;
                PTN = new int[0];
            }
        }

        ChgAccess_Data ChgAccess_Data1 = new ChgAccess_Data(0);

        // E40 data -----------------------------------------------------
        public struct PJ_Data
        {
            //Create Data
            public string PJ_ObjID;
            public int CarrierQuantity;
            public string CarrierID;
            public int SlopNumber;
            public int[] SlotID;
            public int PRRecipeMethod;
            public string RcpSpec;
            public int ProcessStart;

            //run time data
            public int PJState;


            public PJ_Data(int p1)
            {
                CarrierQuantity = p1;
                PJ_ObjID = "";
                CarrierID = "";
                SlopNumber = 0;
                PRRecipeMethod = 0;
                RcpSpec = "";
                ProcessStart = 0;
                SlotID = new int[0];

                PJState = 0;

            }
        }
        PJ_Data PJ_Data1 = new PJ_Data();
        PJ_Data PJ_Data2 = new PJ_Data();

        public struct PJ_Command
        {
            //Command
            public int Command;
            public string PJ_ObjID;

            public PJ_Command(int p1)
            {
                Command = p1;
                PJ_ObjID = "";
            }

        }
        PJ_Command PJ_Command1 = new PJ_Command();

        // E94 data ---------------------------------------------------------
        public struct CJ_Data
        {
            //Create Data
            public string CJ_ObjID;
            public int PrCtrlSpecNumber;
            public string[] PrCtrlSpec_PrObjID;
            public int ProcessOrderMgnt;
            public int StartMethod;

            //runtime data
            public int CJState;

            public CJ_Data(int p1)
            {
                PrCtrlSpecNumber = p1;
                CJ_ObjID = "";
                PrCtrlSpec_PrObjID = new string[0];

                ProcessOrderMgnt = 0;
                StartMethod = 0;

                CJState = 0;
            }

        }
        CJ_Data CJ_Data1 = new CJ_Data();

        public struct CJ_Command
        {
            //Command
            public int Command;
            public string CJ_ObjID;

            public CJ_Command(int p1)
            {
                Command = p1;
                CJ_ObjID = "";
            }

        }
        CJ_Command CJ_Command1 = new CJ_Command();

        //== E87 Standard Const definition =================================================
        public const int LPTSM_0_OUT_OF_SERVICE = 0;
        public const int LPTSM_1_TRANSFER_BLOCKED = 1;
        public const int LPTSM_2_READY_TO_LOAD = 2;
        public const int LPTSM_3_READY_TO_UNLOAD = 3;


        public const int LPTSM_PORT_ID_1 = 1;
        public const int LPTSM_PORT_ID_2 = 2;

        public const int AMSM_0_MANUAL_MODE = 0;
        public const int AMSM_1_AUTO_MODE = 1;

        public const int CARRIER_ID_STATUS_0_ID_NOT_READ = 0;
        public const int CARRIER_ID_STATUS_1_WAITING_FOR_HOST = 1;
        public const int CARRIER_ID_STATUS_2_ID_VERIFICATION_OK = 2;
        public const int CARRIER_ID_STATUS_3_ID_VERIFICATION_FAILED = 3;

        public const int CARRIER_SLOP_MAP_STATUS_0_SLOP_MAP_NOT_READ = 0;
        public const int CARRIER_SLOP_MAP_STATUS_1_WAITING_FOR_HOST = 1;
        public const int CARRIER_SLOP_MAP_STATUS_2_SLOT_MAP_VERIFICATION_OK = 2;
        public const int CARRIER_SLOP_MAP_STATUS_3_SLOT_MAP_VERIFICATION_FAILED = 3;

        public const int CARRIER_SLOP_MAP_0_UNDEFINED = 0;
        public const int CARRIER_SLOP_MAP_1_EMPTY = 1;
        public const int CARRIER_SLOP_MAP_2_NOT_EMPTY = 2;
        public const int CARRIER_SLOP_MAP_3_CORRECTLY_OCCUPIED = 3;
        public const int CARRIER_SLOP_MAP_4_DOUBLE_SLOTTED = 4;
        public const int CARRIER_SLOP_MAP_5_CROSS_SLOTTED = 5;

        public const int CARRIER_ACCESSING_STATUS_0_NOT_ACCESSED = 0;
        public const int CARRIER_ACCESSING_STATUS_1_IN_ACCESS = 1;
        public const int CARRIER_ACCESSING_STATUS_2_CARRIER_COMPLETE = 2;
        public const int CARRIER_ACCESSING_STATUS_3_CARRIER_STOPPED = 3;

        //== E87 Command definition =================================================
        public const int LPTSM_CMD_IN_SERVICE = 1;
        public const int LPTSM_CMD_OUT_SERVICE = 2;
        public const int LPTSM_CMD_LOAD_COMPLETE = 3;
        public const int LPTSM_CMD_UNLOAD_COMPLETE = 4;
        public const int LPTSM_CMD_READY_TO_LOAD = 5;
        public const int LPTSM_CMD_READY_TO_UNLOAD = 6;

        public const int AMSM_CMD_CHANGE_TO_MANUAL = 1;
        public const int AMSM_CMD_CHANGE_TO_AUTO = 2;
        public const int AMSM_CMD_CHANGE_TO_MANUAL_ALL = 3;
        public const int AMSM_CMD_CHANGE_TO_AUTO_ALL = 4;


        public const int CSM_CMD_CARRIER_IS_INSTANTIATED = 1;
        public const int CSM_CMD_CARRIER_ID_READ_SUCCESS = 3;
        public const int CSM_CMD_PROCEED_WITH_CARRIER_RECEIVED = 8;
        public const int CSM_CMD_CANCEL_CARRIER_AT_PORT_RECEIVED = 9;
        public const int CSM_CMD_CANCEL_CARRIER = 10;
        public const int CSM_CMD_CARRIER_RELEASE = 11;

        public const int CSM_CMD_SLOP_MAP_READ_SUCCESS = 14;
        //public const int CSM_CMD_PROCEED_WITH_CARRIER_RECEIVED = 15;
        public const int CSM_CMD_CANCEL_CARRIER_RECEIVED = 16;
        public const int CSM_CMD_CARRIER_START_PROCESS = 18;
        public const int CSM_CMD_CARRIER_PROCESS_COMPLETE = 19;
        public const int CSM_CMD_CARRIER_PROCESS_STOPPED = 20;

        public const int CSM_CMD_UNLOAD_COMPLETE = 21;
        //== E40 Standard Const definition =================================================
        public const int PJSM_NOT_EXIST = -1;
        public const int PJSM_0_QUEUED = 0;
        public const int PJSM_1_SETTING_UP = 1;
        public const int PJSM_2_WAITING_FOR_START = 2;
        public const int PJSM_3_PROCESSING = 3;
        public const int PJSM_4_PROCESS_COMPLETE = 4;
        //Reserved:5
        public const int PJSM_6_PAUSING = 6;
        public const int PJSM_7_PAUSED = 7;
        public const int PJSM_8_STOPPING = 8;
        public const int PJSM_9_ABORTING = 9;

        public const int PRPROCESSSTART_0_MANUAL_START = 0;
        public const int PRPROCESSSTART_1_AUTOMATIC_START = 1;
        //== E40 Command definition =================================================
        public const int PJSM_CMD_RESET = 0;
        public const int PJSM_CMD_ACCEPT_PJ_CREATE = 1;
        public const int PJSM_CMD_SETTING_UP = 2;
        public const int PJSM_CMD_START = 5;
        public const int PJSM_CMD_PROCESS_COMPLETE = 6;
        public const int PJSM_CMD_FOUP_REMOVED = 7;
        public const int PJSM_CMD_PAUSE = 8;
        public const int PJSM_CMD_RESUME = 10;
        public const int PJSM_CMD_STOP = 11;
        public const int PJSM_CMD_ABORT = 13;
        public const int PJSM_CMD_CANCEL = 18;


        //== E94 Standard Const definition =================================================
        public const int CJSM_NOT_EXIST = -1;
        public const int CJSM_0_QUEUED = 0;
        public const int CJSM_1_SELECTED = 1;
        public const int CJSM_2_WAITING_FOR_START = 2;
        public const int CJSM_3_EXECUTING = 3;
        public const int CJSM_4_PAUSED = 4;
        public const int CJSM_5_COMPLETED = 5;


        public const int START_METHOD_0_USER_START = 0;
        public const int START_METHOD_1_AUTO = 1;




        //== E94 Command definition =================================================
        public const int CJ_Command_1_CJStart = 1;
        public const int CJ_Command_2_CJPause = 2;
        public const int CJ_Command_3_CJResume = 3;
        public const int CJ_Command_4_CJCancel = 4;
        public const int CJ_Command_5_CJDeselect = 5;
        public const int CJ_Command_6_CJStop = 6;
        public const int CJ_Command_7_CJAbort = 7;
        public const int CJ_Command_8_CJHOQ = 8;

        public const int CJSM_CMD_RESET = 0;
        public const int CJSM_CMD_ACCEPT_CJ_CREATE = 11;
        public const int CJSM_CMD_PJ_COMPLETE = 10;
        public const int CJSM_CMD_CJ_DELETE = 13;
    }
}