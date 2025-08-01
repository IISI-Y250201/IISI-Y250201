```csharp
/*******************************************************************
程式代碼：icei2047b02
程式名稱：住診即時上傳間隔天數計算邏輯放寬之出院日期及申報訖日空值修正作業
功能簡述：住診即時上傳間隔天數計算邏輯放寬之出院日期及申報訖日空值修正作業
參    數：
參數一：程式代號 分區別
範例一：icei2047b02 1
讀取檔案：
異動檔案：RAPI_ASSAY_DATA_FINAL, ICEI_3060_PBA_CTL
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

namespace icei2047b02
{
    public class icei2047b02
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

        /* ---------- variables ---------- */
        private static string _hPgmId = string.Empty;
        private static string _hBranchCode = string.Empty;
        private static string _startTime = string.Empty;
        private static string _endTime = string.Empty;
        private static string _wkFeeYm = string.Empty;
        private static int _rtnCode = 0;

        /* ---------- structs ---------- */
        private class SQL100
        {
            public string hospId { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string hospDataType { get; set; } = string.Empty;
        }

        private class SQL200
        {
            public string hospId { get; set; } = string.Empty;
            public string hospDataType { get; set; } = string.Empty;
            public string feeYm { get; set; } = string.Empty;
            public string idAes { get; set; } = string.Empty;
            public string birthday { get; set; } = string.Empty;
            public string inDate { get; set; } = string.Empty;
            public string funcSeqNo { get; set; } = string.Empty;
        }

        /* ---------- Main ---------- */
        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder sql = new StringBuilder();
                    sql.AppendLine("SELECT TO_CHAR(SYSDATE, 'YYYY/MM/DD HH24:MI:SS') FROM DUAL");
                    cmd.CommandText = sql.ToString();
                    _startTime = cmd.ExecuteScalar().ToString();
                }

                CheckArg(args);

                _oraConn.Open();

                ProcessHospitalData();

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    StringBuilder sql = new StringBuilder();
                    sql.AppendLine("SELECT TO_CHAR(SYSDATE, 'YYYY/MM/DD HH24:MI:SS') FROM DUAL");
                    cmd.CommandText = sql.ToString();
                    _endTime = cmd.ExecuteScalar().ToString();
                }

                WriteMsg($" {_proList.message}\n ***start_time<{_startTime}>\n ***  end_time<{_endTime}>\n");

                Console.WriteLine("\n程式 icei2047b02 結束\n");
                _proList.exitCode = 0;
                _proList.message = "程式 icei2047b02 結束";
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
        private static void CheckArg(string[] args)
        {
            if (args.Length < 1)
            {
                _proList.exitCode = 1;
                _proList.message = "輸入格式錯誤！提供協助如下：";
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine("\n輸入格式錯誤！提供協助如下：\n");
                Console.WriteLine($"格式:  程式代號    分區");
                Console.WriteLine($"範例: {prog}    1");
                
                _logger.Error(_proList.message);
                throw new ArgumentException();
            }

            if (string.Compare(args[0], "1") < 0 || string.Compare(args[0], "6") > 0)
            {
                _proList.exitCode = 3;
                _proList.message = $"參數：分區<{args[0]}>有誤";
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"\n參數：分區<{args[0]}>有誤\n");
                Console.WriteLine($"格式:  程式代號    分區");
                Console.WriteLine($"範例: {prog}    1");
                
                _logger.Error(_proList.message);
                throw new ArgumentException();
            }

            _hPgmId = AppDomain.CurrentDomain.FriendlyName;
            _hBranchCode = args[0];

            string msg = $"執行參數: {_hPgmId}  {_hBranchCode} \n\n";
            Console.WriteLine(msg);
            _logger.Info(msg);
        }

        private static void ProcessHospitalData()
        {
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                StringBuilder strSQL = new StringBuilder();
                strSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM, 'YYYYMM'), ''");
                strSQL.AppendLine("  FROM ICEI_RAP_ASSAY_CTRL");
                strSQL.AppendLine(" WHERE TO_CHAR(PROC_DATE,'YYYYMMDD') = TRUNC(SYSDATE)");
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                strSQL.AppendLine("   AND WEB_RECV_SEQ != 'CTMRI'");
                strSQL.AppendLine("UNION");
                strSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM, 'YYYYMM'), ''");
                strSQL.AppendLine("  FROM ICEI_3060_PBA_CTL");
                strSQL.AppendLine(" WHERE FEE_YM >= ADD_MONTHS(TO_DATE(TO_CHAR(SYSDATE,'YYYYMM'),'YYYYMM'),-7)");
                strSQL.AppendLine("   AND OUT_DATE_MARK IS NULL");
                strSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE,1,1) = '2'");
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                cmd.Parameters.Add(new OracleParameter("branchCode", _hBranchCode));

                cmd.CommandText = strSQL.ToString();

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string hospId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        string feeYm = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        
                        SQL100 sql100 = new SQL100();
                        
                        using (OracleCommand cmdInner = _oraConn.CreateCommand())
                        {
                            StringBuilder strSQLInner = new StringBuilder();
                            strSQLInner.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM,'YYYYMM'), HOSP_DATA_TYPE");
                            strSQLInner.AppendLine("  FROM RAPI_ASSAY_DATA_FINAL");
                            strSQLInner.AppendLine(" WHERE FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                            cmdInner.Parameters.Add(new OracleParameter("feeYm", feeYm));
                            strSQLInner.AppendLine("   AND HOSP_ID = :hospId");
                            cmdInner.Parameters.Add(new OracleParameter("hospId", hospId));
                            strSQLInner.AppendLine("   AND BRANCH_CODE = :branchCode");
                            cmdInner.Parameters.Add(new OracleParameter("branchCode", _hBranchCode));
                            strSQLInner.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE,1,1) = '2'");
                            strSQLInner.AppendLine("   AND MARK_24HR = '5'");
                            strSQLInner.AppendLine("   AND INTERVAL_MARK IS NULL");
                            strSQLInner.AppendLine("   AND OUT_DATE IS NULL");
                            strSQLInner.AppendLine("   AND APPL_E_DATE IS NULL");
                            strSQLInner.AppendLine("   AND MARK_560 = 'Y'");
                            strSQLInner.AppendLine("   AND FEE_YM_MARK IS NULL");
                            strSQLInner.AppendLine("   AND ROWNUM = 1");

                            cmdInner.CommandText = strSQLInner.ToString();

                            try
                            {
                                using (OracleDataReader readerInner = cmdInner.ExecuteReader())
                                {
                                    if (readerInner.Read())
                                    {
                                        sql100.hospId = readerInner.IsDBNull(0) ? string.Empty : readerInner.GetString(0);
                                        sql100.feeYm = readerInner.IsDBNull(1) ? string.Empty : readerInner.GetString(1);
                                        sql100.hospDataType = readerInner.IsDBNull(2) ? string.Empty : readerInner.GetString(2);
                                    }
                                }
                            }
                            catch (OracleException ex)
                            {
                                _logger.Error($"Error querying RAPI_ASSAY_DATA_FINAL: {ex.Message}");
                                continue;
                            }
                        }

                        Console.WriteLine($"csr_hosp_id 取出 hosp_id<{sql100.hospId}> fee_ym<{sql100.feeYm}> hosp_data_type<{sql100.hospDataType}>");

                        if (!string.IsNullOrEmpty(sql100.hospDataType))
                        {
                            ProcessHospitalDetail(sql100);
                        }
                    }
                }
            }
        }

        private static void ProcessHospitalDetail(SQL100 sql100)
        {
            bool foundHpOutDate = false;

            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                StringBuilder strSQL = new StringBuilder();
                strSQL.AppendLine("SELECT A.HOSP_ID, A.HOSP_DATA_TYPE, TO_CHAR(A.FEE_YM,'YYYYMM'),");
                strSQL.AppendLine("       A.ID_AES, TO_CHAR(A.BIRTHDAY,'YYYYMMDD'), TO_CHAR(A.IN_DATE,'YYYYMMDD'), A.FUNC_SEQ_NO");
                strSQL.AppendLine("  FROM RAPI_ASSAY_DATA_FINAL A");
                strSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                cmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                strSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                cmd.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                strSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE,1,1) = '2'");
                strSQL.AppendLine("   AND MARK_24HR = '5'");
                strSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                strSQL.AppendLine("   AND OUT_DATE IS NULL");
                strSQL.AppendLine("   AND APPL_E_DATE IS NULL");
                strSQL.AppendLine("   AND MARK_560 = 'Y'");
                strSQL.AppendLine("   AND FEE_YM_MARK IS NULL");

                cmd.CommandText = strSQL.ToString();

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SQL200 sql200 = new SQL200
                        {
                            hospId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            hospDataType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            feeYm = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            idAes = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            birthday = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            inDate = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                            funcSeqNo = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
                        };

                        Console.WriteLine($"csr_upd_out_date 取出 hosp_id = {sql200.hospId}, hosp_data_type = {sql200.hospDataType}, fee_ym = {sql200.feeYm}, id_aes = {sql200.idAes}, birthday = {sql200.birthday}, in_date = {sql200.inDate}, func_seq_no = {sql200.funcSeqNo}");

                        string hOutDate = string.Empty;
                        string hApplEDate = string.Empty;

                        using (OracleCommand cmdInner = _oraConn.CreateCommand())
                        {
                            StringBuilder strSQLInner = new StringBuilder();
                            strSQLInner.AppendLine("SELECT TO_CHAR(NVL(OUT_DATE,APPL_E_DATE), 'YYYYMMDD'), TO_CHAR(APPL_E_DATE, 'YYYYMMDD')");
                            strSQLInner.AppendLine("  FROM ICEI_3060_PBA_ORD A");
                            strSQLInner.AppendLine(" WHERE A.HOSP_ID = :hospId");
                            cmdInner.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                            strSQLInner.AppendLine("   AND A.HOSP_DATA_TYPE = :hospDataType");
                            cmdInner.Parameters.Add(new OracleParameter("hospDataType", sql200.hospDataType));
                            strSQLInner.AppendLine("   AND A.FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                            cmdInner.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                            strSQLInner.AppendLine("   AND A.ID_AES = :idAes");
                            cmdInner.Parameters.Add(new OracleParameter("idAes", sql200.idAes));
                            strSQLInner.AppendLine("   AND A.BIRTHDAY = TO_DATE(:birthday, 'YYYYMMDD')");
                            cmdInner.Parameters.Add(new OracleParameter("birthday", sql200.birthday));
                            strSQLInner.AppendLine("   AND A.IN_DATE = TO_DATE(:inDate, 'YYYYMMDD')");
                            cmdInner.Parameters.Add(new OracleParameter("inDate", sql200.inDate));
                            strSQLInner.AppendLine("   AND A.FUNC_SEQ_NO = :funcSeqNo");
                            cmdInner.Parameters.Add(new OracleParameter("funcSeqNo", sql200.funcSeqNo));
                            strSQLInner.AppendLine("   AND A.DATA_TYPE = 'HP'");
                            strSQLInner.AppendLine("   AND NVL(OUT_DATE,APPL_E_DATE) IS NOT NULL");
                            strSQLInner.AppendLine("   AND ROWNUM = 1");

                            cmdInner.CommandText = strSQLInner.ToString();

                            try
                            {
                                using (OracleDataReader readerInner = cmdInner.ExecuteReader())
                                {
                                    if (readerInner.Read())
                                    {
                                        hOutDate = readerInner.IsDBNull(0) ? string.Empty : readerInner.GetString(0);
                                        hApplEDate = readerInner.IsDBNull(1) ? string.Empty : readerInner.GetString(1);
                                    }
                                }
                            }
                            catch (OracleException ex)
                            {
                                _logger.Error($"Error querying ICEI_3060_PBA_ORD: {ex.Message}");
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(hOutDate) || !string.IsNullOrEmpty(hApplEDate))
                        {
                            using (OracleCommand cmdUpdate = _oraConn.CreateCommand())
                            {
                                StringBuilder strSQLUpdate = new StringBuilder();
                                strSQLUpdate.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL A");
                                strSQLUpdate.AppendLine("   SET FEE_YM_MARK = 'Z',");
                                strSQLUpdate.AppendLine("       OUT_DATE = TO_DATE(:outDate,'YYYYMMDD'),");
                                strSQLUpdate.AppendLine("       APPL_E_DATE = TO_DATE(:applEDate,'YYYYMMDD')");
                                strSQLUpdate.AppendLine(" WHERE A.HOSP_ID = :hospId");
                                cmdUpdate.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                                strSQLUpdate.AppendLine("   AND A.HOSP_DATA_TYPE = :hospDataType");
                                cmdUpdate.Parameters.Add(new OracleParameter("hospDataType", sql200.hospDataType));
                                strSQLUpdate.AppendLine("   AND A.FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                                cmdUpdate.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                                strSQLUpdate.AppendLine("   AND A.ID_AES = :idAes");
                                cmdUpdate.Parameters.Add(new OracleParameter("idAes", sql200.idAes));
                                strSQLUpdate.AppendLine("   AND A.BIRTHDAY = TO_DATE(:birthday,'YYYYMMDD')");
                                cmdUpdate.Parameters.Add(new OracleParameter("birthday", sql200.birthday));
                                strSQLUpdate.AppendLine("   AND A.IN_DATE = TO_DATE(:inDate,'YYYYMMDD')");
                                cmdUpdate.Parameters.Add(new OracleParameter("inDate", sql200.inDate));
                                strSQLUpdate.AppendLine("   AND A.FUNC_SEQ_NO = :funcSeqNo");
                                cmdUpdate.Parameters.Add(new OracleParameter("funcSeqNo", sql200.funcSeqNo));

                                cmdUpdate.Parameters.Add(new OracleParameter("outDate", string.IsNullOrEmpty(hOutDate) ? DBNull.Value : (object)hOutDate));
                                cmdUpdate.Parameters.Add(new OracleParameter("applEDate", string.IsNullOrEmpty(hApplEDate) ? DBNull.Value : (object)hApplEDate));

                                cmdUpdate.CommandText = strSQLUpdate.ToString();

                                try
                                {
                                    cmdUpdate.ExecuteNonQuery();
                                    foundHpOutDate = true;
                                }
                                catch (OracleException ex)
                                {
                                    _logger.Error($"Error updating RAPI_ASSAY_DATA_FINAL: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"found_hp_out_date = {foundHpOutDate}");

            if (foundHpOutDate)
            {
                using (OracleCommand cmdUpdate = _oraConn.CreateCommand())
                {
                    StringBuilder strSQLUpdate = new StringBuilder();
                    strSQLUpdate.AppendLine("UPDATE ICEI_3060_PBA_CTL");
                    strSQLUpdate.AppendLine("   SET OUT_DATE_MARK = 'Y'");
                    strSQLUpdate.AppendLine(" WHERE HOSP_ID = :hospId");
                    cmdUpdate.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                    strSQLUpdate.AppendLine("   AND FEE_YM = TO_DATE(:feeYm,'YYYYMM')");
                    cmdUpdate.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                    strSQLUpdate.AppendLine("   AND HOSP_DATA_TYPE = :hospDataType");
                    cmdUpdate.Parameters.Add(new OracleParameter("hospDataType", sql100.hospDataType));

                    cmdUpdate.CommandText = strSQLUpdate.ToString();

                    try
                    {
                        int rowsAffected = cmdUpdate.ExecuteNonQuery();
                        Console.WriteLine($"update ICEI_3060_PBA_CTL, 醫事機構 = {sql100.hospId}, 費用年月 = {sql100.feeYm}, 醫事類別 = {sql100.hospDataType}, 資料筆數 = {rowsAffected}");
                    }
                    catch (OracleException ex)
                    {
                        _logger.Error($"Error updating ICEI_3060_PBA_CTL: {ex.Message}");
                    }
                }

                Ipmt0304ExecIcei2047b01(sql100);
            }

            _oraConn.ExecuteNonQuery("COMMIT");
        }

        private static void Ipmt0304ExecIcei2047b01(SQL100 sql100)
        {
            WriteMsg("-- 460 start ipmt0304_exec_icei2047b01()");

            int feeYmInt = int.Parse(sql100.feeYm) - 191100;
            _wkFeeYm = feeYmInt.ToString();

            // Original: rtn_code = PXX_exec_batch("icei2047b01", sql100.hosp_id, wk_fee_ym, NULL);
            _rtnCode = MEDM_SysLib.MEDM_ExecBatch("icei2047b01", sql100.hospId, _wkFeeYm, null);

            if (_rtnCode != 0)
            {
                string msg = $"PXX_exec_batch( icei2047b01 {sql100.hospId} {_wkFeeYm} ) fail rtn_code:[{_rtnCode}]";
                Console.WriteLine($" {msg}");
                _logger.Error(msg);
                _proList.exitCode = 10;
                _proList.message = "執行失敗";
                throw new Exception(_proList.message);
            }
            else
            {
                string msg = $"PXX_exec_batch( icei2047b01 {sql100.hospId} {_wkFeeYm} ) done rtn_code:[{_rtnCode}]";
                Console.WriteLine($" {msg}");
                _logger.Info(msg);
            }

            WriteMsg("-- 476 end ipmt0304_exec_icei2047b01()");
        }

        private static void WriteMsg(string message)
        {
            Console.WriteLine(message);
            _logger.Info(message);
        }
    }
}
```