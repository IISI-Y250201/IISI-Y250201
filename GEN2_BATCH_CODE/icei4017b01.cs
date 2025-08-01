```csharp
/*******************************************************************
程式代碼：icei4017b01
程式名稱：補外國人身分證
功能簡述：補外國人身分證
參    數：
參數一：程式代號 費用年月起 費用年月迄
範例一：icei4017b01 202301 202312
讀取檔案：
異動檔案：ICEI_3060_PBA_ORD
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

namespace icei4017b01
{
    public class icei4017b01
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

        private static string _feeYmStart = string.Empty;
        private static string _feeYmEnd = string.Empty;
        private static string _sysDate = string.Empty;

        #region SQL Structures
        private class SQL100
        {
            public string hospId { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string branchCode { get; set; } = string.Empty;
        }

        private class SQL200
        {
            public string idAes { get; set; } = string.Empty;
            public string currIdAes { get; set; } = string.Empty;
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

                int cnt = 0;
                string hFeeYm = string.Empty;

                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM,'YYYYMM'), BRANCH_CODE");
                    strSQL.AppendLine("  FROM ICEI_3060_PBA_CTL");
                    strSQL.AppendLine(" WHERE FEE_YM >= TO_DATE(:feeYmStart,'YYYYMM')");
                    cmd.Parameters.Add(new OracleParameter("feeYmStart", _feeYmStart));
                    strSQL.AppendLine("   AND FEE_YM <= TO_DATE(:feeYmEnd,'YYYYMM')");
                    cmd.Parameters.Add(new OracleParameter("feeYmEnd", _feeYmEnd));
                    strSQL.AppendLine("GROUP BY HOSP_ID, FEE_YM, BRANCH_CODE");

                    cmd.CommandText = strSQL.ToString();
                    _oraConn.Open();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cnt = 0;
                            SQL100 sql100 = new SQL100
                            {
                                hospId = reader.GetString(0),
                                feeYm = reader.GetString(1),
                                branchCode = reader.GetString(2)
                            };

                            WriteMsg($"HOSP_ID<{sql100.hospId}>\n" +
                                     $"FEE_YM<{sql100.feeYm}>\n" +
                                     $"BRANCH_CODE<{sql100.branchCode}>");

                            StringBuilder strSQL2 = new StringBuilder();
                            using (OracleCommand cmd2 = _oraConn.CreateCommand())
                            {
                                strSQL2.AppendLine("SELECT DISTINCT A.ID_AES, D.CURR_ID_AES");
                                strSQL2.AppendLine("  FROM ICEI_3060_PBA_ORD A, ( SELECT C.PREV_ID_AES, C.CURR_ID_AES");
                                strSQL2.AppendLine("                                FROM ( SELECT PREV_ID_AES");
                                strSQL2.AppendLine("                                         FROM MHBT_MAPID_AES");
                                strSQL2.AppendLine("                                       GROUP BY PREV_ID_AES");
                                strSQL2.AppendLine("                                       HAVING COUNT(*) = 1 ) B, MHBT_MAPID_AES C");
                                strSQL2.AppendLine("                               WHERE B.PREV_ID_AES = C.PREV_ID_AES ) D");
                                strSQL2.AppendLine(" WHERE FEE_YM   = TO_DATE(:feeYm,'YYYYMM')");
                                cmd2.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                                strSQL2.AppendLine("   AND HOSP_ID  = :hospId");
                                cmd2.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                strSQL2.AppendLine("   AND A.ID_AES = D.PREV_ID_AES");
                                strSQL2.AppendLine("UNION ALL");
                                strSQL2.AppendLine("SELECT DISTINCT A.ID_AES, D.PREV_ID_AES");
                                strSQL2.AppendLine("  FROM ICEI_3060_PBA_ORD A, ( SELECT C.PREV_ID_AES, C.CURR_ID_AES");
                                strSQL2.AppendLine("                                FROM ( SELECT PREV_ID_AES");
                                strSQL2.AppendLine("                                         FROM MHBT_MAPID_AES");
                                strSQL2.AppendLine("                                       GROUP BY PREV_ID_AES");
                                strSQL2.AppendLine("                                       HAVING COUNT(*) = 1 ) B, MHBT_MAPID_AES C");
                                strSQL2.AppendLine("                               WHERE B.PREV_ID_AES = C.PREV_ID_AES ) D");
                                strSQL2.AppendLine(" WHERE FEE_YM   = TO_DATE(:feeYm2,'YYYYMM')");
                                cmd2.Parameters.Add(new OracleParameter("feeYm2", sql100.feeYm));
                                strSQL2.AppendLine("   AND HOSP_ID  = :hospId2");
                                cmd2.Parameters.Add(new OracleParameter("hospId2", sql100.hospId));
                                strSQL2.AppendLine("   AND A.ID_AES = D.CURR_ID_AES");

                                cmd2.CommandText = strSQL2.ToString();
                                using (OracleDataReader reader2 = cmd2.ExecuteReader())
                                {
                                    while (reader2.Read())
                                    {
                                        SQL200 sql200 = new SQL200
                                        {
                                            idAes = reader2.GetString(0),
                                            currIdAes = reader2.GetString(1)
                                        };

                                        StringBuilder updateSQL = new StringBuilder();
                                        using (OracleCommand updateCmd = _oraConn.CreateCommand())
                                        {
                                            updateSQL.AppendLine("UPDATE ICEI_3060_PBA_ORD");
                                            updateSQL.AppendLine("   SET NEW_ID_AES = :idAes");
                                            updateCmd.Parameters.Add(new OracleParameter("idAes", sql200.idAes));
                                            updateSQL.AppendLine(" WHERE FEE_YM  = TO_DATE(:feeYm,'YYYYMM')");
                                            updateCmd.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                                            updateSQL.AppendLine("   AND HOSP_ID = :hospId");
                                            updateCmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                            updateSQL.AppendLine("   AND ID_AES  = :currIdAes");
                                            updateCmd.Parameters.Add(new OracleParameter("currIdAes", sql200.currIdAes));

                                            updateCmd.CommandText = updateSQL.ToString();
                                            updateCmd.ExecuteNonQuery();
                                        }

                                        cnt++;
                                    }
                                }
                            }

                            if (cnt > 0)
                            {
                                Console.WriteLine(sql100.feeYm);
                                hFeeYm = (int.Parse(sql100.feeYm) - 191100).ToString();
                                WriteMsg($"呼叫IPM執行程式：icei3061b01 {sql100.branchCode} {hFeeYm} {sql100.hospId}");
                                // Original: PXX_exec_batch("icei3061b01", sql100.branch_code, h_fee_ym, sql100.hosp_id, NULL);
                                MEDM_SysLib.MEDM_ExecBatch("icei3061b01", sql100.branchCode, hFeeYm, sql100.hospId);
                                cnt = 0;
                            }

                            using (OracleCommand commitCmd = _oraConn.CreateCommand())
                            {
                                commitCmd.CommandText = "COMMIT";
                                commitCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                WriteMsg("程式執行完成");

                _proList.exitCode = 0;
                _proList.message = $"\n程式 icei4017b01 結束\n";
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
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
                // Original: PXX_exit_process(rtn_code, 0, msg);
                MEDM_SysLib.MEDM_ExitProcess(_proList.exitCode, _proList.message);
            }
        }

        /* ---------- parameter check ---------- */
        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length != 2)
            {
                _proList.exitCode = 1;
                _proList.message = "參數個數不符";
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"用法：{prog} 費用年月起 費用年月迄");
                Console.WriteLine($"範例：{prog} 202301 202312");
                
                _logger.Error(_proList.message);
                throw new ArgumentException(_proList.message);
            }

            _feeYmStart = args[0];
            _feeYmEnd = args[1];

            if (_feeYmStart.Length != 6 || _feeYmEnd.Length != 6)
            {
                _proList.exitCode = 9;
                _proList.message = $"費用年月(起)<{_feeYmStart}> 費用年月(迄)<{_feeYmEnd}>";
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"用法：{prog} 費用年月起 費用年月迄");
                Console.WriteLine($"範例：{prog} 202301 202312");
                
                _logger.Error(_proList.message);
                throw new ArgumentException(_proList.message);
            }

            WriteMsg($"費用年月<起>：{_feeYmStart}\n" +
                     $"費用年月<迄>：{_feeYmEnd}");

            _logger.Info($"Args → {string.Join(",", args)}");
        }

        #region Helper Methods
        // Original: get_sysdate()
        private static void GetSystemDate()
        {
            _sysDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        // Original: write_msg()
        private static void WriteMsg(string message)
        {
            GetSystemDate();
            string formattedMsg = $"<{_sysDate}>\n{message}";
            Console.WriteLine(formattedMsg);
            _logger.Info(message);
        }
        #endregion
    }
}
```