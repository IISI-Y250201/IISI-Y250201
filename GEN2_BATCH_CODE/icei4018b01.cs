```csharp
/*******************************************************************
程式代碼：icei4018b01
程式名稱：外國人身分證自動化
功能簡述：更新外國人身分證資料
參    數：
參數一：執行日期
範例一：icei4018b01 20231212
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

namespace icei4018b01
{
    public class icei4018b01
    {
        /* ---------- static members ---------- */
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

        private static string _gDate = string.Empty;
        private static string _hChkFeeYm = string.Empty;
        private static string _hSysDate = string.Empty;

        #region SQL Structures
        private class SQL100
        {
            public string prevIdAes { get; set; } = string.Empty;
            public string currIdAes { get; set; } = string.Empty;
        }

        private class SQL200
        {
            public string branchCode { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string hospId { get; set; } = string.Empty;
        }

        private class SQL300
        {
            public string hospId { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
        }
        #endregion

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                WriteMsg("程式開始執行");

                CheckArg(args);

                // 系統日-1個月的費用年月
                _hChkFeeYm = string.Empty;
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT TO_CHAR(ADD_MONTHS(SYSDATE,-1),'YYYYMM')");
                    strSQL.AppendLine("FROM DUAL");

                    cmd.CommandText = strSQL.ToString();
                    _hChkFeeYm = cmd.ExecuteScalar().ToString();
                }

                WriteMsg($"系統日前1個月之費用年月：{_hChkFeeYm}");

                // Process MHBT_MAPID_AES records
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT PREV_ID_AES, CURR_ID_AES");
                    strSQL.AppendLine("FROM MHBT_MAPID_AES");
                    strSQL.AppendLine("WHERE ADD_TIME >= TO_DATE(:gDate,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("gDate", _gDate));

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL100 sql100 = new SQL100
                            {
                                prevIdAes = reader["PREV_ID_AES"].ToString(),
                                currIdAes = reader["CURR_ID_AES"].ToString()
                            };

                            // Update ICEI_3060_PBA_ORD - NEW_ID_AES
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                StringBuilder updateSQL = new StringBuilder();
                                updateSQL.AppendLine("UPDATE ICEI_3060_PBA_ORD");
                                updateSQL.AppendLine("SET NEW_ID_AES = :currIdAes,");
                                updateSQL.AppendLine("    CONVER_ID_DATE = SYSDATE");
                                updateSQL.AppendLine("WHERE FEE_YM = TO_DATE(:hChkFeeYm,'YYYYMM')");
                                updateSQL.AppendLine("AND ID_AES = :prevIdAes");
                                updateSQL.AppendLine("AND NEW_ID_AES IS NULL");

                                updateCmd.Parameters.Add(new OracleParameter("currIdAes", sql100.currIdAes));
                                updateCmd.Parameters.Add(new OracleParameter("hChkFeeYm", _hChkFeeYm));
                                updateCmd.Parameters.Add(new OracleParameter("prevIdAes", sql100.prevIdAes));

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    WriteMsg($"new_id_aes = {sql100.currIdAes} 更新 {rowsAffected}筆");
                                }

                                // Update ICEI_3060_PBA_ORD - NEW1_ID_AES
                                StringBuilder updateSQL2 = new StringBuilder();
                                updateSQL2.AppendLine("UPDATE ICEI_3060_PBA_ORD");
                                updateSQL2.AppendLine("SET NEW1_ID_AES = :currIdAes,");
                                updateSQL2.AppendLine("    CONVER_ID_DATE = SYSDATE");
                                updateSQL2.AppendLine("WHERE FEE_YM = TO_DATE(:hChkFeeYm,'YYYYMM')");
                                updateSQL2.AppendLine("AND ID_AES = :prevIdAes");
                                updateSQL2.AppendLine("AND NEW_ID_AES <> :currIdAes");
                                updateSQL2.AppendLine("AND NEW1_ID_AES IS NULL");

                                updateCmd.Parameters.Clear();
                                updateCmd.Parameters.Add(new OracleParameter("currIdAes", sql100.currIdAes));
                                updateCmd.Parameters.Add(new OracleParameter("hChkFeeYm", _hChkFeeYm));
                                updateCmd.Parameters.Add(new OracleParameter("prevIdAes", sql100.prevIdAes));
                                updateCmd.Parameters.Add(new OracleParameter("currIdAes", sql100.currIdAes));

                                updateCmd.CommandText = updateSQL2.ToString();
                                rowsAffected = updateCmd.ExecuteNonQuery();

                                if (rowsAffected > 0)
                                {
                                    WriteMsg($"new1_id_aes = {sql100.currIdAes} 更新 {rowsAffected}筆");
                                }
                            }
                        }
                    }
                }

                // Commit changes
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = "COMMIT";
                    cmd.ExecuteNonQuery();
                }

                // Process ICEI_3060_PBA_ORD records for batch execution
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT BRANCH_CODE, TO_CHAR(FEE_YM,'YYYYMM')-191100, HOSP_ID");
                    strSQL.AppendLine("FROM ICEI_3060_PBA_ORD");
                    strSQL.AppendLine("WHERE CONVER_ID_DATE = SYSDATE");
                    strSQL.AppendLine("AND FEE_YM = TO_DATE(:hChkFeeYm,'YYYYMM')");
                    strSQL.AppendLine("GROUP BY BRANCH_CODE, TO_CHAR(FEE_YM,'YYYYMM')-191100, HOSP_ID");
                    cmd.Parameters.Add(new OracleParameter("hChkFeeYm", _hChkFeeYm));

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL200 sql200 = new SQL200
                            {
                                branchCode = reader[0].ToString(),
                                feeYm = reader[1].ToString(),
                                hospId = reader[2].ToString()
                            };

                            WriteMsg($"呼叫IPM執行程式：icei3061b01 {sql200.branchCode} {sql200.feeYm} {sql200.hospId}");
                            // Original: PXX_exec_batch("icei3061b01", sql200.branch_code, sql200.fee_ym, sql200.hosp_id, NULL);
                            MEDM_SysLib.MEDM_ExecBatch("icei3061b01", sql200.branchCode, sql200.feeYm, sql200.hospId, null);
                        }
                    }
                }

                // Process ICEI_3060_PBA_CTL records
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM,'YYYYMM')");
                    strSQL.AppendLine("FROM ICEI_3060_PBA_CTL");
                    strSQL.AppendLine("WHERE CREATE_DATE = SYSDATE-1");
                    strSQL.AppendLine("GROUP BY HOSP_ID, FEE_YM");

                    cmd.CommandText = strSQL.ToString();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL300 sql300 = new SQL300
                            {
                                hospId = reader[0].ToString(),
                                feeYm = reader[1].ToString()
                            };

                            // Update ICEI_3060_PBA_ORD - clear NEW1_ID_AES
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                StringBuilder updateSQL = new StringBuilder();
                                updateSQL.AppendLine("UPDATE ICEI_3060_PBA_ORD");
                                updateSQL.AppendLine("SET NEW1_ID_AES = NULL");
                                updateSQL.AppendLine("WHERE FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                                updateSQL.AppendLine("AND HOSP_ID = :hospId");
                                updateSQL.AppendLine("AND NEW_ID_AES = NEW1_ID_AES");

                                updateCmd.Parameters.Add(new OracleParameter("feeYm", sql300.feeYm));
                                updateCmd.Parameters.Add(new OracleParameter("hospId", sql300.hospId));

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                WriteMsg($"HOSP_ID<{sql300.hospId}>\nFEE_YM<{sql300.feeYm}>\n清空new1_id_aes {rowsAffected}筆");
                            }
                        }
                    }
                }

                // Commit changes
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = "COMMIT";
                    cmd.ExecuteNonQuery();
                }

                WriteMsg("程式執行完成");

                _proList.exitCode = 0;
                _proList.message = $"\n程式 icei4018b01 結束\n";
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

        /* ---------- parameter check ---------- */
        private static void CheckArg(string[] args)
        {
            if (args.Length != 1)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"參數種類：   程式代號   執行日期");
                Console.WriteLine($"範例    ： {prog}  20231212");
                Console.WriteLine($"執行日期：SYS-表示系統日-1日");
                
                throw new ArgumentException();
            }

            _gDate = string.Empty;
            if (args[0] == "SYS")
            {
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder strSQL = new StringBuilder();
                    strSQL.AppendLine("SELECT TO_CHAR(SYSDATE-1, 'YYYYMMDD')");
                    strSQL.AppendLine("FROM DUAL");

                    cmd.CommandText = strSQL.ToString();
                    _gDate = cmd.ExecuteScalar().ToString();
                }
            }
            else if (args[0].Length != 8)
            {
                _proList.exitCode = 9;
                _proList.message = $"輸入日期格式有誤<{args[0]}>";
                WriteMsg(_proList.message);
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"參數種類：   程式代號   執行日期");
                Console.WriteLine($"範例    ： {prog}  20231212");
                Console.WriteLine($"執行日期：SYS-表示系統日-1日");
                
                throw new ArgumentException();
            }
            else
            {
                _gDate = args[0];
            }

            WriteMsg($"執行日期：{_gDate}");
            _logger.Info($"Args → {string.Join(",", args)}");
        }

        #region Helper Methods
        private static void GetSysDate()
        {
            _hSysDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        private static void WriteMsg(string message)
        {
            GetSysDate();
            string formattedMsg = $"<{_hSysDate}>\n{message}";
            Console.WriteLine(formattedMsg);
            _logger.Info(formattedMsg);
        }
        #endregion
    }
}
```