```csharp
/*******************************************************************
程式代碼：icei4016b01
程式名稱：代上傳檢驗查之院所需重新執行檢核程式
功能簡述：代上傳檢驗查之院所若晚於原院所費用申報時做上傳，會沒辦法自動啟動icei3061b01的檢核去檢核原費用申報之院所
參    數：
參數一：執行日期
範例一：icei4016b01 20230101
讀取檔案：無
異動檔案：無
作    者：
歷次修改時間：
1.
需求單號暨修改內容簡述：
1.
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei4016b01
{
    public class icei4016b01
    {
        #region Static Members
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());
        private static string _chkDate = string.Empty;
        private static string _sysDate = string.Empty;
        #endregion

        #region Structs
        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }

        private class SQL200
        {
            public string branchCode { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
        }
        #endregion

        #region Main
        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                WriteMsg("程式開始執行");

                CheckArg(args);

                for (int mon = 11; mon >= 0; mon--)
                {
                    StringBuilder strSQL = new StringBuilder();
                    using (OracleCommand cmd = _oraConn.CreateCommand())
                    {
                        strSQL.AppendLine("SELECT DISTINCT B.BRANCH_CODE, TO_CHAR(A.FEE_YM,'YYYYMM')-191100 FEE_YM, B.HOSP_ID");
                        strSQL.AppendLine("FROM ( SELECT BRANCH_CODE, ORIG_HOSP_ID, HOSP_ID, FEE_YM");
                        strSQL.AppendLine("       FROM RAPI_ASSAY_DATA_FINAL");
                        strSQL.AppendLine("       WHERE FEE_YM = ADD_MONTHS(TO_DATE(SUBSTR(:chkDate,1,6),'YYYYMM'),:mon)");
                        cmd.Parameters.Add(new OracleParameter("chkDate", _chkDate));
                        cmd.Parameters.Add(new OracleParameter("mon", -mon));
                        strSQL.AppendLine("         AND ORIG_HOSP_ID IS NOT NULL");
                        strSQL.AppendLine("         AND ORIG_HOSP_ID != HOSP_ID");
                        strSQL.AppendLine("         AND ( TRUNC(EXE_E_DATE_D) = TRUNC(TO_DATE(:chkDateFull,'YYYYMMDD')) OR");
                        strSQL.AppendLine("               TRUNC(EXE_E_DATE_M) = TRUNC(TO_DATE(:chkDateFull2,'YYYYMMDD')) )");
                        cmd.Parameters.Add(new OracleParameter("chkDateFull", _chkDate));
                        cmd.Parameters.Add(new OracleParameter("chkDateFull2", _chkDate));
                        strSQL.AppendLine("       GROUP BY BRANCH_CODE, ORIG_HOSP_ID, HOSP_ID, FEE_YM ) A, MHAT_HOSPBSC B");
                        strSQL.AppendLine("WHERE A.ORIG_HOSP_ID = B.HOSP_ID");

                        WriteMsg($"chk_date<{_chkDate}> mon<{mon}>");

                        cmd.CommandText = strSQL.ToString();
                        using (OracleDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                SQL200 sql200 = new SQL200
                                {
                                    branchCode = reader["BRANCH_CODE"].ToString(),
                                    feeYm = reader["FEE_YM"].ToString(),
                                    hospId = reader["HOSP_ID"].ToString()
                                };

                                WriteMsg($"BRANCH_CODE<{sql200.branchCode}>\n" +
                                         $"FEE_YM<{sql200.feeYm}>\n" +
                                         $"HOSP_ID<{sql200.hospId}>");

                                // Original: PXX_exec_batch
                                MEDM_SysLib.MEDM_ExecBatch("icei3061b01", sql200.branchCode, sql200.feeYm, sql200.hospId, null);
                            }
                        }
                    }
                }

                WriteMsg("程式執行完成");

                string msg = "\n程式 icei4016b01 結束\n";
                Console.WriteLine(msg);
                _proList.exitCode = 0;
                _proList.message = msg;
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
                _proList.message = ex.Message;
                Console.WriteLine(ex.ToString());
                _logger.Error(ex.ToString());
            }
            finally
            {
                // Original: PXX_exit_process
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }
        #endregion

        #region Methods
        private static ProList _proList = new ProList();

        // Original: get_sysdate()
        private static void GetSysDate()
        {
            _sysDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Original: write_msg()
        private static void WriteMsg(string format, params object[] args)
        {
            string message = string.Format(format, args);
            GetSysDate();
            string formattedMsg = $"<{_sysDate}>\n{message}";
            Console.WriteLine(formattedMsg);
            _logger.Info(formattedMsg);
        }

        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length != 1)
            {
                _proList.exitCode = 1;
                ShowUsage();
                throw new ArgumentException("參數個數不符");
            }

            if (args[0] == "SYS")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT TO_CHAR(SYSDATE-1,'YYYYMMDD') FROM DUAL");
                    cmd.CommandText = strSQL.ToString();
                    _chkDate = cmd.ExecuteScalar().ToString();
                }
            }
            else if (args[0].CompareTo("20100101") >= 0 && args[0].CompareTo("29101231") <= 0)
            {
                _chkDate = args[0];
            }
            else
            {
                _proList.exitCode = 9;
                WriteMsg($"執行日期<{args[0]}>有誤");
                ShowUsage();
                throw new ArgumentException($"執行日期<{args[0]}>有誤");
            }

            WriteMsg($"執行日期<{_chkDate}>");
        }

        private static void ShowUsage()
        {
            string usage = 
                $"參數種類：   程式代號  執行日期 \n" +
                $"範例    ： icei4016b01 20230101 \n" +
                $"執行日期：SYS時表示SYSDATE \n";
            Console.WriteLine(usage);
            _logger.Error(usage);
        }
        #endregion
    }
}
```