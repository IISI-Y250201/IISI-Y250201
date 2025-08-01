```csharp
/*******************************************************************
程式代碼：icei2047b04
程式名稱：住診即時上傳間隔天數計算邏輯放寬之出院日期及申報訖日空值修正作業
功能簡述：住診即時上傳間隔天數計算邏輯放寬之出院日期及申報訖日空值修正作業
參    數：
參數一：程式代號 分區別 費用年月(西元)
範例一：icei2047b04 1 202301
讀取檔案：
異動檔案：
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

namespace icei2047b04
{
    public class icei2047b04
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

        // Program parameters
        private static string _pgmId = string.Empty;
        private static string _feeYm = string.Empty;
        private static string _branchCode = string.Empty;

        private static string _startTime = string.Empty;
        private static string _endTime = string.Empty;
        private static string _wkFeeYm = string.Empty;
        #endregion

        #region Structs
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
        #endregion

        static void Main(string[] args)
        {
            try
            {
                // Original: PXX_start_process();
                MEDM_SysLib.MEDM_StartProcess();

                // Get system date
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TO_CHAR(SYSDATE, 'yyyy/mm/dd hh24:mi:ss') FROM DUAL";
                    _startTime = cmd.ExecuteScalar().ToString();
                }

                CheckArg(args);

                _oraConn.Open();

                ProcessHospitalData();

                // Call IPM execution program
                WriteMsg($"呼叫IPM執行程式：icei2047b03 {_branchCode} {_feeYm}\n");
                // Original: PXX_exec_batch("icei2047b03", h_branch_code, h_fee_ym, NULL);
                MEDM_SysLib.MEDM_ExecBatch("icei2047b03", _branchCode, _feeYm, null);

                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT TO_CHAR(SYSDATE, 'yyyy/mm/dd hh24:mi:ss') FROM DUAL";
                    _endTime = cmd.ExecuteScalar().ToString();
                }

                WriteMsg($" \n ***start_time<{_startTime}>\n ***  end_time<{_endTime}>\n");

                _proList.exitCode = 0;
                _proList.message = "\n程式 icei2047b04 結束\n";
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

        private static void ProcessHospitalData()
        {
            StringBuilder strSQL = new StringBuilder();
            using (OracleCommand cmd = _oraConn.CreateCommand())
            {
                // Get hospital IDs
                strSQL.Clear();
                strSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM, 'YYYYMM'), ''");
                strSQL.AppendLine("  FROM ICEI_3060_PBA_CTL");
                strSQL.AppendLine(" WHERE FEE_YM = TO_DATE(:feeYm, 'yyyymm')");
                strSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE, 1, 1) = '2'");
                strSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                strSQL.AppendLine(" GROUP BY HOSP_ID, FEE_YM");
                cmd.Parameters.Add(new OracleParameter("feeYm", _feeYm));
                cmd.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                cmd.CommandText = strSQL.ToString();

                using (OracleDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string hospId = reader.GetString(0);
                        string feeYm = reader.GetString(1);
                        
                        SQL100 sql100 = new SQL100();

                        // Check if hospital has data in RAPI_ASSAY_DATA_FINAL
                        using (OracleCommand cmdCheck = _oraConn.CreateCommand())
                        {
                            StringBuilder checkSQL = new StringBuilder();
                            checkSQL.AppendLine("SELECT HOSP_ID, TO_CHAR(FEE_YM, 'YYYYMM'), HOSP_DATA_TYPE");
                            checkSQL.AppendLine("  FROM RAPI_ASSAY_DATA_FINAL");
                            checkSQL.AppendLine(" WHERE FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                            checkSQL.AppendLine("   AND HOSP_ID = :hospId");
                            checkSQL.AppendLine("   AND BRANCH_CODE = :branchCode");
                            checkSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE, 1, 1) = '2'");
                            checkSQL.AppendLine("   AND MARK_24HR = '5'");
                            checkSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                            checkSQL.AppendLine("   AND OUT_DATE IS NULL");
                            checkSQL.AppendLine("   AND APPL_E_DATE IS NULL");
                            checkSQL.AppendLine("   AND MARK_560 = 'Y'");
                            checkSQL.AppendLine("   AND FEE_YM_MARK IS NULL");
                            checkSQL.AppendLine("   AND ROWNUM = 1");
                            cmdCheck.Parameters.Add(new OracleParameter("feeYm", feeYm));
                            cmdCheck.Parameters.Add(new OracleParameter("hospId", hospId));
                            cmdCheck.Parameters.Add(new OracleParameter("branchCode", _branchCode));
                            cmdCheck.CommandText = checkSQL.ToString();

                            using (OracleDataReader checkReader = cmdCheck.ExecuteReader())
                            {
                                if (checkReader.Read())
                                {
                                    sql100.hospId = checkReader.GetString(0);
                                    sql100.feeYm = checkReader.GetString(1);
                                    sql100.hospDataType = checkReader.GetString(2);
                                }
                            }
                        }

                        Console.WriteLine($"csr_hosp_id 取出 hosp_id<{sql100.hospId}> fee_ym<{sql100.feeYm}> hosp_data_type<{sql100.hospDataType}>");

                        if (!string.IsNullOrEmpty(sql100.hospDataType))
                        {
                            // Update ICEI_3060_PBA_CTL
                            using (OracleCommand cmdUpdate = _oraConn.CreateCommand())
                            {
                                StringBuilder updateSQL = new StringBuilder();
                                updateSQL.AppendLine("UPDATE ICEI_3060_PBA_CTL");
                                updateSQL.AppendLine("   SET OUT_DATE_MARK = ''");
                                updateSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                                updateSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                                updateSQL.AppendLine("   AND HOSP_DATA_TYPE = :hospDataType");
                                cmdUpdate.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                cmdUpdate.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                                cmdUpdate.Parameters.Add(new OracleParameter("hospDataType", sql100.hospDataType));
                                cmdUpdate.CommandText = updateSQL.ToString();
                                cmdUpdate.ExecuteNonQuery();
                            }

                            bool foundHpOutDate = false;

                            // Get records to update
                            using (OracleCommand cmdGetRecords = _oraConn.CreateCommand())
                            {
                                StringBuilder recordsSQL = new StringBuilder();
                                recordsSQL.AppendLine("SELECT A.HOSP_ID, A.HOSP_DATA_TYPE, TO_CHAR(A.FEE_YM, 'YYYYMM'),");
                                recordsSQL.AppendLine("       A.ID_AES, TO_CHAR(A.BIRTHDAY, 'YYYYMMDD'), TO_CHAR(A.IN_DATE, 'YYYYMMDD'), A.FUNC_SEQ_NO");
                                recordsSQL.AppendLine("  FROM RAPI_ASSAY_DATA_FINAL A");
                                recordsSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                                recordsSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                                recordsSQL.AppendLine("   AND SUBSTR(HOSP_DATA_TYPE, 1, 1) = '2'");
                                recordsSQL.AppendLine("   AND MARK_24HR = '5'");
                                recordsSQL.AppendLine("   AND INTERVAL_MARK IS NULL");
                                recordsSQL.AppendLine("   AND OUT_DATE IS NULL");
                                recordsSQL.AppendLine("   AND APPL_E_DATE IS NULL");
                                recordsSQL.AppendLine("   AND MARK_560 = 'Y'");
                                recordsSQL.AppendLine("   AND FEE_YM_MARK IS NULL");
                                recordsSQL.AppendLine(" GROUP BY A.HOSP_ID, A.HOSP_DATA_TYPE, TO_CHAR(A.FEE_YM, 'YYYYMM'),");
                                recordsSQL.AppendLine("          A.ID_AES, TO_CHAR(A.BIRTHDAY, 'YYYYMMDD'), TO_CHAR(A.IN_DATE, 'YYYYMMDD'), A.FUNC_SEQ_NO");
                                cmdGetRecords.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                cmdGetRecords.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                                cmdGetRecords.CommandText = recordsSQL.ToString();

                                using (OracleDataReader recordsReader = cmdGetRecords.ExecuteReader())
                                {
                                    while (recordsReader.Read())
                                    {
                                        SQL200 sql200 = new SQL200
                                        {
                                            hospId = recordsReader.GetString(0),
                                            hospDataType = recordsReader.GetString(1),
                                            feeYm = recordsReader.GetString(2),
                                            idAes = recordsReader.GetString(3),
                                            birthday = recordsReader.GetString(4),
                                            inDate = recordsReader.GetString(5),
                                            funcSeqNo = recordsReader.GetString(6)
                                        };

                                        Console.WriteLine($"csr_upd_out_date 取出 hosp_id = {sql200.hospId}, hosp_data_type = {sql200.hospDataType}, fee_ym = {sql200.feeYm}, id_aes = {sql200.idAes}, birthday = {sql200.birthday}, in_date = {sql200.inDate}, func_seq_no = {sql200.funcSeqNo}");

                                        string outDate = string.Empty;
                                        string applEDate = string.Empty;

                                        // Get out_date and appl_e_date from ICEI_3060_PBA_ORD
                                        using (OracleCommand cmdGetDates = _oraConn.CreateCommand())
                                        {
                                            StringBuilder datesSQL = new StringBuilder();
                                            datesSQL.AppendLine("SELECT TO_CHAR(NVL(OUT_DATE, APPL_E_DATE), 'YYYYMMDD'), TO_CHAR(APPL_E_DATE, 'YYYYMMDD')");
                                            datesSQL.AppendLine("  FROM ICEI_3060_PBA_ORD A");
                                            datesSQL.AppendLine(" WHERE A.HOSP_ID = :hospId");
                                            datesSQL.AppendLine("   AND A.HOSP_DATA_TYPE = :hospDataType");
                                            datesSQL.AppendLine("   AND A.FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                                            datesSQL.AppendLine("   AND A.ID_AES = :idAes");
                                            datesSQL.AppendLine("   AND A.BIRTHDAY = TO_DATE(:birthday, 'YYYYMMDD')");
                                            datesSQL.AppendLine("   AND A.IN_DATE = TO_DATE(:inDate, 'YYYYMMDD')");
                                            datesSQL.AppendLine("   AND A.FUNC_SEQ_NO = :funcSeqNo");
                                            datesSQL.AppendLine("   AND A.DATA_TYPE = 'HP'");
                                            datesSQL.AppendLine("   AND NVL(OUT_DATE, APPL_E_DATE) IS NOT NULL");
                                            datesSQL.AppendLine("   AND ROWNUM = 1");
                                            cmdGetDates.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                                            cmdGetDates.Parameters.Add(new OracleParameter("hospDataType", sql200.hospDataType));
                                            cmdGetDates.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                                            cmdGetDates.Parameters.Add(new OracleParameter("idAes", sql200.idAes));
                                            cmdGetDates.Parameters.Add(new OracleParameter("birthday", sql200.birthday));
                                            cmdGetDates.Parameters.Add(new OracleParameter("inDate", sql200.inDate));
                                            cmdGetDates.Parameters.Add(new OracleParameter("funcSeqNo", sql200.funcSeqNo));
                                            cmdGetDates.CommandText = datesSQL.ToString();

                                            using (OracleDataReader datesReader = cmdGetDates.ExecuteReader())
                                            {
                                                if (datesReader.Read())
                                                {
                                                    outDate = datesReader.IsDBNull(0) ? string.Empty : datesReader.GetString(0);
                                                    applEDate = datesReader.IsDBNull(1) ? string.Empty : datesReader.GetString(1);
                                                }
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(outDate) || !string.IsNullOrEmpty(applEDate))
                                        {
                                            // Update RAPI_ASSAY_DATA_FINAL
                                            using (OracleCommand cmdUpdateRapi = _oraConn.CreateCommand())
                                            {
                                                StringBuilder updateRapiSQL = new StringBuilder();
                                                updateRapiSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL A");
                                                updateRapiSQL.AppendLine("   SET FEE_YM_MARK = 'Z',");
                                                updateRapiSQL.AppendLine("       OUT_DATE = TO_DATE(:outDate, 'YYYYMMDD'),");
                                                updateRapiSQL.AppendLine("       APPL_E_DATE = TO_DATE(:applEDate, 'YYYYMMDD')");
                                                updateRapiSQL.AppendLine(" WHERE A.HOSP_ID = :hospId");
                                                updateRapiSQL.AppendLine("   AND A.HOSP_DATA_TYPE = :hospDataType");
                                                updateRapiSQL.AppendLine("   AND A.FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                                                updateRapiSQL.AppendLine("   AND A.ID_AES = :idAes");
                                                updateRapiSQL.AppendLine("   AND A.BIRTHDAY = TO_DATE(:birthday, 'YYYYMMDD')");
                                                updateRapiSQL.AppendLine("   AND A.IN_DATE = TO_DATE(:inDate, 'YYYYMMDD')");
                                                updateRapiSQL.AppendLine("   AND A.FUNC_SEQ_NO = :funcSeqNo");
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("outDate", outDate));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("applEDate", applEDate));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("hospId", sql200.hospId));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("hospDataType", sql200.hospDataType));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("feeYm", sql200.feeYm));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("idAes", sql200.idAes));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("birthday", sql200.birthday));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("inDate", sql200.inDate));
                                                cmdUpdateRapi.Parameters.Add(new OracleParameter("funcSeqNo", sql200.funcSeqNo));
                                                cmdUpdateRapi.CommandText = updateRapiSQL.ToString();
                                                cmdUpdateRapi.ExecuteNonQuery();

                                                foundHpOutDate = true;
                                            }
                                        }
                                    }
                                }
                            }

                            Console.WriteLine($"found_hp_out_date = {foundHpOutDate}");

                            if (foundHpOutDate)
                            {
                                // Update ICEI_3060_PBA_CTL
                                using (OracleCommand cmdUpdateCtl = _oraConn.CreateCommand())
                                {
                                    StringBuilder updateCtlSQL = new StringBuilder();
                                    updateCtlSQL.AppendLine("UPDATE ICEI_3060_PBA_CTL");
                                    updateCtlSQL.AppendLine("   SET OUT_DATE_MARK = 'Y'");
                                    updateCtlSQL.AppendLine(" WHERE HOSP_ID = :hospId");
                                    updateCtlSQL.AppendLine("   AND FEE_YM = TO_DATE(:feeYm, 'YYYYMM')");
                                    updateCtlSQL.AppendLine("   AND HOSP_DATA_TYPE = :hospDataType");
                                    cmdUpdateCtl.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                    cmdUpdateCtl.Parameters.Add(new OracleParameter("feeYm", sql100.feeYm));
                                    cmdUpdateCtl.Parameters.Add(new OracleParameter("hospDataType", sql100.hospDataType));
                                    cmdUpdateCtl.CommandText = updateCtlSQL.ToString();
                                    int rowsAffected = cmdUpdateCtl.ExecuteNonQuery();

                                    Console.WriteLine($"update ICEI_3060_PBA_CTL, 醫事機構 = {sql100.hospId}, 費用年月 = {sql100.feeYm}, 醫事類別 = {sql100.hospDataType}, 資料筆數 = {rowsAffected}");
                                }
                            }
                        }

                        // Commit transaction
                        using (OracleCommand cmdCommit = _oraConn.CreateCommand())
                        {
                            cmdCommit.CommandText = "COMMIT";
                            cmdCommit.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        // Original: ipmt0304_exec_icei2047b01()
        private static int Ipmt0304ExecIcei2047b01(string hospId, string feeYm)
        {
            WriteMsg("-- 460 start ipmt0304_exec_icei2047b01()");

            int rtnCode = 0;
            int feeYmInt = int.Parse(feeYm) - 191100;
            _wkFeeYm = feeYmInt.ToString();

            // Original: rtn_code = PXX_exec_batch("icei2047b01", sql100.hosp_id, wk_fee_ym, NULL);
            rtnCode = MEDM_SysLib.MEDM_ExecBatch("icei2047b01", hospId, _wkFeeYm, null);

            if (rtnCode != 0)
            {
                string msg = $"PXX_exec_batch( icei2047b01 {hospId} {_wkFeeYm} ) fail rtn_code:[{rtnCode}]";
                Console.WriteLine($" {msg}");
                _logger.Error(msg);
                // Original: PXX_exit_process(10, 0, "執行失敗");
                MEDM_SysLib.MEDM_ExitProcess(10, "執行失敗");
            }
            else
            {
                string msg = $"PXX_exec_batch( icei2047b01 {hospId} {_wkFeeYm} ) done rtn_code:[{rtnCode}]";
                Console.WriteLine($" {msg}");
                _logger.Error(msg);
            }

            WriteMsg("-- 476 end ipmt0304_exec_icei2047b01()");

            return rtnCode;
        }

        // Original: write_msg()
        private static void WriteMsg(string format, params object[] args)
        {
            string msg = string.Format(format, args);
            Console.Write(msg);
            _logger.Info(msg);
        }

        // Original: check_arg()
        private static void CheckArg(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("\n輸入格式錯誤！提供協助如下：\n");
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"格式:  程式代號    分區  費用年月(西元)");
                Console.WriteLine($"範例: {prog}    1    202301");
                
                _proList.exitCode = 1;
                _proList.message = "icei2047b04";
                throw new ArgumentException("參數個數不符");
            }

            if (string.Compare(args[1], "1") < 0 || string.Compare(args[1], "6") > 0)
            {
                Console.WriteLine($"\n參數：分區<{args[1]}>有誤\n");
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"格式:  程式代號    分區  費用年月(西元)");
                Console.WriteLine($"範例: {prog}    1    202301");
                
                _proList.exitCode = 3;
                throw new ArgumentException("分區參數有誤");
            }

            if (string.Compare(args[2], "201901") < 0 || string.Compare(args[2], "291001") > 0)
            {
                Console.WriteLine($"\n參數：費用年月<{args[2]}>有誤\n");
                
                var prog = AppDomain.CurrentDomain.FriendlyName;
                Console.WriteLine($"格式:  程式代號    分區  費用年月(西元)");
                Console.WriteLine($"範例: {prog}    1    202301");
                
                _proList.exitCode = 3;
                throw new ArgumentException("費用年月參數有誤");
            }

            _pgmId = args[0];
            _branchCode = args[1];
            _feeYm = args[2];

            string msg = $"執行參數: {_pgmId} {_branchCode} {_feeYm} \n\n";
            Console.Write(msg);
            _logger.Info(msg);
        }
    }
}
```