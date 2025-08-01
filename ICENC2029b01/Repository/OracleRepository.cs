using ICENC2029b01.Repository.Interface;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using ICENC2029b01.Models;
using NLog;
using Med3Core.CB.MED.Common;

namespace ICENC2029b01.Repository
{
    public class OracleRepository : IOracleRepository
    {
        private readonly ActivitySource _activitySource = new ActivitySource("OpenTelemetrySource");
        private static Logger _logger = LogManager.GetLogger(AppDomain.CurrentDomain.FriendlyName.Substring(0, 3).ToUpper());

        private static ProList _proList = new ProList();

        private static string _sInputFeeYm = string.Empty;
        private static string _sInputHospId = string.Empty;
        private static string _sValidSDate = string.Empty;
        private static string _sMark24hr = string.Empty;
        private static string _wkBranchCode = string.Empty;
        private static string _sExecFlag = string.Empty;
        private static int _iExeType = 1;

        /// <summary>
        /// </summary>
        /// <param name="argDto"></param>
        /// <param name="oraConn"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public int ProcessData(ArgDto argDto, ref OracleConnection _oraConn)
        {
            int insTot = 0;
            try
            {
                _sInputFeeYm = argDto._sInputFeeYm ?? string.Empty;
                _sInputHospId = argDto._sInputHospId ?? string.Empty;
                _sValidSDate = argDto._sValidSDate ?? string.Empty;
                _sMark24hr = argDto._sMark24hr ?? string.Empty;
                _wkBranchCode = argDto._wkBranchCode ?? string.Empty;
                _sExecFlag = argDto._sExecFlag ?? string.Empty;


                if (_sExecFlag == "0")
                {
                    UpdateMark24Hr(ref _oraConn);
                }

                if (_sExecFlag == "1")
                {
                    UpdateMark560(ref _oraConn);
                }

                if (_sExecFlag == "2")
                {
                    UpdateMark24hrPBA560(ref _oraConn);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }
        }

        private static void UpdateMark24Hr(ref OracleConnection _oraConn)
        {
            int iUpdateCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - UpdateMark24Hr() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] ");

            try
            {
                if (_oraConn.State == ConnectionState.Closed)
                {
                    _oraConn.Open();
                }


                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT code hosp_id,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMMDD') recv_s_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMMDD') recv_e_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_S_DATE,'YYYYMM')||'01' fee_s_date,");
                    strSQL.AppendLine("       TO_CHAR(VALID_E_DATE,'YYYYMM')||'01' fee_e_date");
                    strSQL.AppendLine("  FROM pxxt_code");
                    strSQL.AppendLine(" WHERE sub_sys   = 'ICE'");
                    strSQL.AppendLine("   AND data_type = '109'");
                    strSQL.AppendLine("   AND TRUNC(TO_DATE(:sInputFeeYm,'YYYYMMDD'),'MM') BETWEEN TRUNC(valid_s_date,'MM') AND TRUNC(valid_e_date,'MM')");
                    cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                    strSQL.AppendLine("   AND ( code      = :sInputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                    strSQL.AppendLine("         'ALL'     = UPPER(:sInputHospIdUpper) OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                    strSQL.AppendLine("         ( NVL(:sInputHospIdNull,0) = 0 AND");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                    strSQL.AppendLine("           exe_time >= ( SELECT EXE_TIME");
                    strSQL.AppendLine("                           FROM pxxt_code");
                    strSQL.AppendLine("                          WHERE sub_sys   = 'ICE'");
                    strSQL.AppendLine("                            AND data_type = '110'");
                    strSQL.AppendLine("                            AND code = '1' ) ) )");

                    cmd.CommandText = strSQL.ToString();

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SQL100 sql100 = new SQL100
                            {
                                hospId = reader["hosp_id"].ToString(),
                                recvSDate = reader["recv_s_date"].ToString(),
                                recvEDate = reader["recv_e_date"].ToString(),
                                feeSDate = reader["fee_s_date"].ToString(),
                                feeEDate = reader["fee_e_date"].ToString()
                            };

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                                updateSQL.AppendLine("   SET MARK_24HR = '8'");
                                updateSQL.AppendLine(" WHERE ( a.hosp_id = :hospId OR :hospIdZero = '0000000000' )");
                                updateCmd.Parameters.Add(new OracleParameter("hospId", sql100.hospId));
                                updateCmd.Parameters.Add(new OracleParameter("hospIdZero", sql100.hospId));
                                updateSQL.AppendLine("   AND a.fee_ym BETWEEN TO_DATE(:feeSDate,'YYYYMMDD') AND TO_DATE(:feeEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("feeSDate", sql100.feeSDate));
                                updateCmd.Parameters.Add(new OracleParameter("feeEDate", sql100.feeEDate));
                                updateSQL.AppendLine("   AND a.CASE_TIME BETWEEN TO_DATE(:recvSDate,'YYYYMMDD') AND TO_DATE(:recvEDate,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("recvSDate", sql100.recvSDate));
                                updateCmd.Parameters.Add(new OracleParameter("recvEDate", sql100.recvEDate));
                                updateSQL.AppendLine("   AND a.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND a.recv_D_NUM > 0");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string message = $"    update hosp_id:[{sql100.hospId}] fee_s_date:[{sql100.feeSDate}] fee_e_date:[{sql100.feeEDate}] " +
                                                $"recv_s_date:[{sql100.recvSDate}] recv_e_date:[{sql100.recvEDate}] sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(message);
                                _logger.Info(message);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }

                                iUpdateCount++;
                            }
                        }
                    }
                }

                string countMessage = $"    update pxxt_code s_input_hosp_id:[{_sInputHospId}] i_update_count:[{iUpdateCount}] ";
                Console.WriteLine(countMessage);
                _logger.Info(countMessage);

                // Update pxxt_code if needed
                if (iUpdateCount > 0 &&
                    (string.IsNullOrEmpty(_sInputHospId) ||
                     _sInputHospId.ToUpper() == "ALL"))
                {
                    StringBuilder updatePxxtSQL = new StringBuilder();
                    using (OracleCommand updatePxxtCmd = _oraConn.CreateCommand())
                    {
                        updatePxxtSQL.AppendLine("UPDATE pxxt_code");
                        updatePxxtSQL.AppendLine("   SET EXE_TIME = SYSDATE");
                        updatePxxtSQL.AppendLine(" WHERE sub_sys = 'ICE'");
                        updatePxxtSQL.AppendLine("   AND data_type = '110'");
                        updatePxxtSQL.AppendLine("   AND code = '1'");

                        updatePxxtCmd.CommandText = updatePxxtSQL.ToString();
                        int rowsAffected = updatePxxtCmd.ExecuteNonQuery();

                        string message = $"update pxxt_code ICE data_type='110' code='1' sqlcode:[0] rec:[{rowsAffected}] ";
                        Console.WriteLine(message);
                        _logger.Info(message);

                        // Commit the update
                        using (OracleCommand commitCmd = _oraConn.CreateCommand())
                        {
                            commitCmd.CommandText = "COMMIT";
                            commitCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }

        private static void UpdateMark560(ref OracleConnection _oraConn)
        {
            try
            {
                if (_oraConn.State == ConnectionState.Closed)
                {
                    _oraConn.Open();
                }


                // Get hospital IDs
                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT hosp_id");
                    strSQL.AppendLine("  FROM mhat_hospbsc a");
                    strSQL.AppendLine(" WHERE EXISTS ( SELECT 1");
                    strSQL.AppendLine("                  FROM RAPI_ASSAY_DATA_FINAL b");
                    strSQL.AppendLine("                 WHERE a.hosp_id = b.hosp_id");
                    strSQL.AppendLine("                   AND b.fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                    cmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                    strSQL.AppendLine("                   AND ( hosp_id = :sInputHospId OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                    strSQL.AppendLine("                         'ALL' = UPPER(:sInputHospIdUpper) OR");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                    strSQL.AppendLine("                         NVL(:sInputHospIdNull,0) = 0 )");
                    cmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                    strSQL.AppendLine("               )");
                    strSQL.AppendLine(" ORDER BY hosp_id ASC");

                    cmd.CommandText = strSQL.ToString();

                    Console.WriteLine($"    1 - UpdateMark560() - select mhat_hospbsc s_exec_flag:[{_sExecFlag}] s_input_fee_ym:[{_sInputFeeYm}] s_input_hosp_id(選項):[{_sInputHospId}]");

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        int iHospIdCount = 0;
                        while (reader.Read())
                        {
                            string hospId = reader["hosp_id"].ToString();
                            string message = $"    update hosp_id[{iHospIdCount}]:[{hospId}] ";
                            Console.Write(message);
                            _logger.Info(message);

                            // Update RAPI_ASSAY_DATA_FINAL for each hospital
                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL");
                                updateSQL.AppendLine("   SET mark_560='Y'");
                                updateSQL.AppendLine(" WHERE hosp_id = :hospId");
                                updateCmd.Parameters.Add(new OracleParameter("hospId", hospId));
                                updateSQL.AppendLine("   AND fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                                updateSQL.AppendLine("   AND mark_560 IS NULL");
                                updateSQL.AppendLine("   AND order_code IN (");
                                updateSQL.AppendLine("       SELECT code FROM pxxt_code");
                                updateSQL.AppendLine("        WHERE sub_sys = 'PBA' AND data_type = '560'");
                                updateSQL.AppendLine("        UNION");
                                updateSQL.AppendLine("       SELECT '64164B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64169B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64202B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64170B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64162B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64258B' code FROM dual UNION");
                                updateSQL.AppendLine("       SELECT '64201B' code FROM dual )");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string resultMessage = $" sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(resultMessage);
                                _logger.Info(resultMessage);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }
                            }

                            iHospIdCount++;
                        }

                        Console.WriteLine($"        i_hosp_id_count:[{iHospIdCount}] sqlcode:[0] rec:[{iHospIdCount}] ");
                    }
                }
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }

        private static void UpdateMark24hrPBA560(ref OracleConnection _oraConn)
        {
            int iLoopCount = 0;
            _iExeType = 1;

            Console.WriteLine($"    0 - UpdateMark24hrPBA560() - select pxxt_code s_exec_flag:[{_sExecFlag}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] ");

            try
            {
                if (_oraConn.State == ConnectionState.Closed)
                {
                    _oraConn.Open();
                }


                StringBuilder strSQL = new StringBuilder();
                using (OracleCommand cmd = _oraConn.CreateCommand())
                {
                    strSQL.AppendLine("SELECT code order_code");
                    strSQL.AppendLine("  FROM pxxt_code");
                    strSQL.AppendLine(" WHERE sub_sys = 'PBA'");
                    strSQL.AppendLine("   AND data_type = '560'");
                    strSQL.AppendLine("   AND valid_s_date = TO_DATE(:validSDate,'yyyymmdd')");
                    cmd.Parameters.Add(new OracleParameter("validSDate", _sValidSDate));

                    cmd.CommandText = strSQL.ToString();

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string orderCode = reader["order_code"].ToString();

                            string message = $"    update s_mark_24hr:[{_sMark24hr}] s_input_hosp_id:[{_sInputHospId}] s_input_fee_ym:[{_sInputFeeYm}] wk_order_code:[{orderCode}] wk_branch_code:[{_wkBranchCode}] ";
                            Console.WriteLine(message);
                            _logger.Info(message);

                            StringBuilder updateSQL = new StringBuilder();
                            using (OracleCommand updateCmd = _oraConn.CreateCommand())
                            {
                                updateSQL.AppendLine("UPDATE RAPI_ASSAY_DATA_FINAL a");
                                updateSQL.AppendLine("   SET MARK_24HR = :sMark24hr");
                                updateCmd.Parameters.Add(new OracleParameter("sMark24hr", _sMark24hr));
                                updateSQL.AppendLine(" WHERE ( a.hosp_id = :sInputHospId OR UPPER(:sInputHospIdUpper) = 'ALL' OR NVL(:sInputHospIdNull,0)=0 )");
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospId", _sInputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospIdUpper", _sInputHospId));
                                updateCmd.Parameters.Add(new OracleParameter("sInputHospIdNull", string.IsNullOrEmpty(_sInputHospId) ? "0" : _sInputHospId));
                                updateSQL.AppendLine("   AND a.fee_ym = TO_DATE(:sInputFeeYm,'YYYYMMDD')");
                                updateCmd.Parameters.Add(new OracleParameter("sInputFeeYm", _sInputFeeYm));
                                updateSQL.AppendLine("   AND a.order_code = :orderCode");
                                updateCmd.Parameters.Add(new OracleParameter("orderCode", orderCode));
                                updateSQL.AppendLine("   AND a.BRANCH_CODE = ( CASE WHEN :wkBranchCode BETWEEN '1' AND '6' THEN :wkBranchCodeValue ELSE a.BRANCH_CODE END )");
                                updateCmd.Parameters.Add(new OracleParameter("wkBranchCode", _wkBranchCode));
                                updateCmd.Parameters.Add(new OracleParameter("wkBranchCodeValue", _wkBranchCode));
                                updateSQL.AppendLine("   AND a.MARK_24HR = '5'");
                                updateSQL.AppendLine("   AND TO_DATE(SUBSTR(a.INIT_WEB_RECV_SEQ,1,7)+19110000,'YYYYMMDD') < LAST_DAY(ADD_MONTHS(A.fee_ym,1))+1");

                                updateCmd.CommandText = updateSQL.ToString();
                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                string resultMessage = $"    sqlcode:[0] rec:[{rowsAffected}] ";
                                Console.WriteLine(resultMessage);
                                _logger.Info(resultMessage);

                                // Commit after each update
                                using (OracleCommand commitCmd = _oraConn.CreateCommand())
                                {
                                    commitCmd.CommandText = "COMMIT";
                                    commitCmd.ExecuteNonQuery();
                                }

                                iLoopCount++;
                            }
                        }
                    }
                }

                string countMessage = $"    update RAPI_ASSAY_DATA_FINAL i_loop_count:[{iLoopCount}] ";
                Console.WriteLine(countMessage);
                _logger.Info(countMessage);
            }
            finally
            {
                if (_oraConn.State == ConnectionState.Open)
                {
                    _oraConn.Close();
                }
            }
        }
    }
}
