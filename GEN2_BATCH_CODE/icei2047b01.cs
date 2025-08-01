```csharp
/*******************************************************************
程式代碼：icei2047b01
程式名稱：住診即時上傳間隔天數計算邏輯放寬作業
功能簡述：住診即時上傳間隔天數計算邏輯放寬作業
參    數：
參數一：程式代號 醫事機構代碼 費用年月(民國)
範例一：icei2047b01 0401180014 11010
讀取檔案：無
異動檔案：無
作    者：AI Assistant
歷次修改時間：
1.2023/11/01
需求單號暨修改內容簡述：
1.住診即時上傳間隔天數計算邏輯放寬作業
備    註：
********************************************************************/

using System;
using System.Data;
using System.Text;
using NLog;
using Oracle.ManagedDataAccess.Client;
using NHI.MEDCS.MEDM.Common;

namespace icei2047b01
{
    public class icei2047b01
    {
        #region Static Members
        private static OracleConnection _oraConn = new OracleConnection(GetDBInfo.GetHmDBConnectString);
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private class ProList
        {
            public int exitCode = -999;
            public string message = string.Empty;
        }
        private static ProList _proList = new ProList();

        private static string _hospId = string.Empty;
        private static string _feeYm = string.Empty;
        private static string _feeYm3061 = string.Empty;
        private static string _branchCode = string.Empty;
        private static string _startTime = string.Empty;
        private static string _endTime = string.Empty;
        private static int _updRecd = 0;
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                GetSystemDate();
                CheckArg(args);

                GetBranchCode();

                if (_branchCode.CompareTo("1") >= 0 && _branchCode.CompareTo("6") <= 0)
                {
                    WriteMsg($"hosp_id<{_hospId}> fee_ym<{_feeYm}> branch_code<{_branchCode}>\n");

                    _updRecd = 0;

                    UpdateMarkD();
                    UpdateMarkE();
                    UpdateMarkA1();
                    UpdateMarkA2();
                    UpdateMarkB1();
                    UpdateMarkB2();
                    UpdateMarkC1();
                    UpdateMarkC2();

                    Console.WriteLine($"--267 upd_recd<{_updRecd}>");

                    if (_updRecd >= 1)
                    {
                        UpdateAssayDl6Mst();
                    }

                    _feeYm3061 = (int.Parse(_feeYm) - 191100).ToString();
                    
                    // 呼叫IPM執行程式
                    WriteMsg($"呼叫IPM執行程式：icei3061b01 {_branchCode} {_feeYm3061} {_hospId}\n");
                    
                    // Original: PXX_exec_batch("icei3061b01", h_branch_code, h_fee_ym_3061, h_hosp_id, NULL);
                    MEDM_SysLib.MEDM_ExecBatch("icei3061b01", _branchCode, _feeYm3061, _hospId, null);
                }
                else
                {
                    _proList.exitCode = 9;
                    WriteMsg($"擷取分區別有誤<{_branchCode}>\n");
                    throw new Exception($"擷取分區別有誤<{_branchCode}>");
                }

                CommitTransaction();
                GetSystemEndTime();
                WriteMsg($" {_proList.message}\n ***start_time<{_startTime}>\n ***  end_time<{_endTime}>\n");

                _proList.exitCode = 0;
                _proList.message = "\n程式 icei2047b01 結束\n";
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
        private static void GetSystemDate()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT TO_CHAR(SYSDATE, 'YYYY/MM/DD HH24:MI:SS') FROM DUAL");
                cmd.CommandText = strSQL.ToString();
                _startTime = cmd.ExecuteScalar().ToString();
            }
        }

        private static void GetSystemEndTime()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT TO_CHAR(SYSDATE, 'YYYY/MM/DD HH24:MI:SS') FROM DUAL");
                cmd.CommandText = strSQL.ToString();
                _endTime = cmd.ExecuteScalar().ToString();
            }
        }

        private static void GetBranchCode()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("SELECT BRANCH_CODE");
                strSQL.AppendLine("  FROM RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine(" WHERE FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND ROWNUM = 1");

                cmd.CommandText = strSQL.ToString();
                object result = cmd.ExecuteScalar();
                _branchCode = result != null ? result.ToString() : string.Empty;
            }
        }

        private static void CommitTransaction()
        {
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                cmd.CommandText = "COMMIT";
                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateMarkD()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'D'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TREAT_DT > CASE_TIME)");
                strSQL.AppendLine("   AND TREAT_DT > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-TREAT_DT <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkE()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'E'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND (RECARD_MARK = '2' AND TREAT_DT > CASE_TIME)");
                strSQL.AppendLine("   AND TREAT_DT BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkA1()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'A'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-REAL_RECV_DATE <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkA2()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'A'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME > NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-CASE_TIME <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkB1()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'B'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE < IN_DATE");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkB2()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'B'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME < IN_DATE");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkC1()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'C'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NOT NULL");
                strSQL.AppendLine("   AND REAL_RECV_DATE BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateMarkC2()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                strSQL.AppendLine("   SET MARK_24HR = 'C'");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                strSQL.AppendLine("   AND CASE_REPORT_TYPE IN ('1','2','3')");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND HOSP_DATA_TYPE IN ('22','21','29')");
                strSQL.AppendLine("   AND (TXT_MARK IS NULL OR TXT_MARK='3')");
                strSQL.AppendLine("   AND REAL_RECV_DATE IS NULL");
                strSQL.AppendLine("   AND CASE_TIME BETWEEN IN_DATE AND NVL(OUT_DATE,APPL_E_DATE)");
                strSQL.AppendLine("   AND TO_DATE(SUBSTR(WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD')-NVL(OUT_DATE,APPL_E_DATE) <= 2");

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    _updRecd++;
                }
            }
        }

        private static void UpdateAssayDl6Mst()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                strSQL.AppendLine("UPDATE ICEI_ASSAY_DL6_MST");
                strSQL.AppendLine("   SET ASSAY_STATUS = NULL");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", _hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));

                cmd.CommandText = strSQL.ToString();
                int rowsAffected = cmd.ExecuteNonQuery();
                Console.WriteLine($"update icei_assay_dl6_mst, 醫事機構 = {_hospId}, 費用年月 = {_feeYm}, 資料筆數 = {rowsAffected}");
            }
        }

        private static void WriteMsg(string message)
        {
            _proList.message = message;
            Console.Write(message);
            _logger.Info(message);
        }
        #endregion

        #region Parameter Check
        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length != 2)
            {
                _proList.exitCode = 1;
                ShowUsage();
                throw new ArgumentException("參數個數不符");
            }

            if (args[0].Length != 10)
            {
                _proList.exitCode = 3;
                WriteMsg($"醫事機構代碼有誤<{args[0]}>\n");
                ShowUsage();
                throw new ArgumentException($"醫事機構代碼有誤<{args[0]}>");
            }
            _hospId = args[0];

            if (args[1].CompareTo("11010") < 0 || args[1].Length != 5)
            {
                _proList.exitCode = 3;
                WriteMsg($"費用年月(民國)有誤<{args[1]}>\n");
                ShowUsage();
                throw new ArgumentException($"費用年月(民國)有誤<{args[1]}>");
            }

            // Convert Taiwan year to Gregorian year
            _feeYm = (int.Parse(args[1]) + 191100).ToString();

            WriteMsg($"參數1：<{args[0]}>\n參數2：<{args[1]}>\n");
        }

        private static void ShowUsage()
        {
            string usage = "參數種類：   程式代號  醫事機構代碼  費用年月(民國) \n" +
                          $"範例    ： icei2047b01  0401180014      11010       \n";
            Console.WriteLine(usage);
        }
        #endregion
    }
}
```