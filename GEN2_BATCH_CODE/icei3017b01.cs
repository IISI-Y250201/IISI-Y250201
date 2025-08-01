```csharp
/*******************************************************************
程式代碼：icei3017b01
程式名稱：補112年以前申報比檢驗(查)明細醫令
功能簡述：每天可以呼叫 112年以前 收載的醫令進行檢核
參    數：
參數一：執行日期
範例一：icei3017b01 1130101
讀取檔案：無
異動檔案：無
作    者：
歷次修改時間：
1.113.04.10
需求單號暨修改內容簡述：
1.新增PROC 每天可以呼叫 112年以前 收載的醫令進行檢核
備    註：醫令(ICMI0130-ICE-020)
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei3017b01
{
    public class icei3017b01
    {
        #region Static Members
        private static OracleConnection _oraConn = 
            new OracleConnection(GetDBInfo.GetHmDBConnectString);
        
        private static Logger _logger = 
            LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());
        
        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();
        
        private static string _hExeDate = string.Empty;
        private static string _hSysDate = string.Empty;
        #endregion

        #region Struct Declarations
        private class SQL100
        {
            public string pgmId { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
            public string hospDataType { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string applType { get; set; } = string.Empty;
            public string applDate { get; set; } = string.Empty;
        }
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();
                
                WriteMsg("程式開始執行");
                
                CheckArg(args);
                
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT PGM_ID, HOSP_ID, HOSP_DATA_TYPE, TO_CHAR(FEE_YM,'YYYYMM')-191100, APPL_TYPE, TO_CHAR(APPL_DATE,'YYYYMMDD')-19110000");
                    strSQL.AppendLine("  FROM ( SELECT 'icei3061b06' PGM_ID, A.HOSP_ID, A.HOSP_DATA_TYPE, A.FEE_YM, A.APPL_TYPE, A.APPL_DATE");
                    strSQL.AppendLine("           FROM ICEI_3060_PBA_ORD A, ICEI_RAP_ASSAY_CTRL B");
                    strSQL.AppendLine("          WHERE A.HOSP_ID = B.HOSP_ID");
                    strSQL.AppendLine("            AND A.FEE_YM  = B.FEE_YM");
                    strSQL.AppendLine("            AND A.FEE_YM <= TO_DATE('202212','YYYYMM')");
                    strSQL.AppendLine("            AND A.ORDER_CODE IN ( SELECT C.CODE");
                    strSQL.AppendLine("                                    FROM PXXT_CODE C");
                    strSQL.AppendLine("                                   WHERE SUB_SYS    = 'ICE'");
                    strSQL.AppendLine("                                     AND DATA_TYPE  = '020'");
                    strSQL.AppendLine("                                     AND CODE_CNAME = '1'");
                    strSQL.AppendLine("                                     AND A.FEE_YM BETWEEN C.VALID_S_DATE AND C.VALID_E_DATE )");
                    strSQL.AppendLine("            AND B.PROC_DATE >= TO_DATE(:hExeDate,'YYYYMMDD') -1");
                    cmd.Parameters.Add(new OracleParameter("hExeDate", _hExeDate));
                    strSQL.AppendLine("            AND nvl(A_AUDIT_QTY,0) = 0 )");
                    strSQL.AppendLine("GROUP BY PGM_ID, HOSP_ID, HOSP_DATA_TYPE, FEE_YM, APPL_TYPE, APPL_DATE");
                    
                    cmd.CommandText = strSQL.ToString();
                    
                    _oraConn.Open();
                    
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL100 sql100 = new SQL100
                            {
                                pgmId = reader.GetString(0),
                                hospId = reader.GetString(1),
                                hospDataType = reader.GetString(2),
                                feeYm = reader.GetString(3),
                                applType = reader.GetString(4),
                                applDate = reader.GetString(5)
                            };
                            
                            WriteMsg($"PXX_exec_batch pgm_id<{sql100.pgmId}> hosp_id<{sql100.hospId}> hosp_data_type<{sql100.hospDataType}> fee_ym<{sql100.feeYm}> appl_type<{sql100.applType}> appl_date<{sql100.applDate}> send_rea<N>");
                            
                            // Original: rtn_code = PXX_exec_batch(sql100.pgm_id,sql100.hosp_id, sql100.hosp_data_type, sql100.fee_ym, sql100.appl_type, sql100.appl_date, "N", NULL );
                            int rtnCode = MEDM_SysLib.MEDM_ExecBatch(sql100.pgmId, sql100.hospId, sql100.hospDataType, sql100.feeYm, sql100.applType, sql100.applDate, "N", null);
                            
                            if (rtnCode != 0)
                            {
                                WriteMsg($"PXX_exec_batch 執行失敗 return[{rtnCode}]");
                                _proList.exitCode = 30;
                                _proList.message = "PXX_exec_batch 執行失敗";
                                throw new Exception(_proList.message);
                            }
                        }
                    }
                    
                    _oraConn.Close();
                }
                
                WriteMsg("程式執行完成");
                
                _proList.exitCode = 0;
                _proList.message = $"\n程式 icei3017b01 結束\n";
                Console.WriteLine(_proList.message);
            }
            catch (OracleException ex)
            {
                _proList.exitCode = 200;
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            catch (Exception ex)
            {
                if (_proList.exitCode == -999)
                {
                    _proList.exitCode = 99;
                }
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            finally
            {
                // Original: PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        #region Helper Methods
        // Original: get_sysdate()
        private static void GetSystemDate()
        {
            _hSysDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Original: write_msg(char *fmt, ...)
        private static void WriteMsg(string message)
        {
            GetSystemDate();
            string formattedMsg = $"<{_hSysDate}>\n{message}";
            Console.WriteLine(formattedMsg);
            _logger.Info(formattedMsg);
        }

        // Original: check_arg(int argc, char *argv[])
        private static void CheckArg(string[] args)
        {
            string usage = 
$@"參數種類：   程式代號   執行日期
範例    ： icei3017b01   1130101
執行日期：SYS-表系統日";

            if (args.Length != 1)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                Console.WriteLine(usage);
                _logger.Error(usage);
                throw new ArgumentException(_proList.message);
            }

            string exeDate = args[0];
            
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                StringBuilder strSQL = new StringBuilder();
                
                if (exeDate == "SYS")
                {
                    strSQL.AppendLine("SELECT TO_CHAR(SYSDATE,'YYYYMMDD') FROM DUAL");
                }
                else
                {
                    strSQL.AppendLine("SELECT TO_CHAR(TO_NUMBER(:exeDate)+19110000) FROM DUAL");
                    cmd.Parameters.Add(new OracleParameter("exeDate", exeDate));
                }
                
                cmd.CommandText = strSQL.ToString();
                
                _oraConn.Open();
                _hExeDate = cmd.ExecuteScalar().ToString();
                _oraConn.Close();
            }
            
            WriteMsg($"參數：{exeDate} h_exe_date<{_hExeDate}>");
            
            _logger.Info($"Args → {string.Join(",", args)}");
        }
        #endregion
    }
}
```