﻿using Com.DanLiris.Service.Purchasing.Lib.Facades.DebtAndDispositionSummary;
using Com.DanLiris.Service.Purchasing.Lib.PDFTemplates;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Com.DanLiris.Service.Purchasing.WebApi.Controllers.v1.Reports
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/reports/debt-and-disposition-summaries")]
    [Authorize]
    public class DebtAndDispositionController : Controller
    {
        private readonly IDebtAndDispositionSummaryService _service;
        private readonly IdentityService _identityService;
        private const string ApiVersion = "1.0";

        public DebtAndDispositionController(IServiceProvider serviceProvider)
        {
            _service = serviceProvider.GetService<IDebtAndDispositionSummaryService>();
            _identityService = serviceProvider.GetService<IdentityService>();
        }

        private void VerifyUser()
        {
            _identityService.Username = User.Claims.ToArray().SingleOrDefault(p => p.Type.Equals("username")).Value;
            _identityService.Token = Request.Headers["Authorization"].FirstOrDefault().Replace("Bearer ", "");
            _identityService.TimezoneOffset = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
        }

        [HttpGet]
        public IActionResult Get([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue;
                var result = _service.GetReport(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);
                return Ok(new
                {
                    apiVersion = ApiVersion,
                    statusCode = General.OK_STATUS_CODE,
                    message = General.OK_MESSAGE,
                    data = result,
                    info = new Dictionary<string, object>
                    {
                        { "page", 1 },
                        { "size", 10 }
                    },
                });
            }
            catch (Exception e)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, e.Message + " " + e.StackTrace);
            }
        }

        [HttpGet("download-excel")]
        public IActionResult DownloadExcel([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = GenerateExcel(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault().AddHours(_identityService.TimezoneOffset), accountingUnitId, isImport, isForeignCurrency, divisionId);

                var filename = "Laporan Rekap Hutang & Disposisi Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Rekap Hutang & Disposisi Lokal Valas";
                else if (isImport)
                    filename = "Laporan Rekap Hutang & Disposisi Import";
                filename += ".xlsx";

                var bytes = stream.ToArray();

                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }
       
        private MemoryStream GenerateExcel(List<DebtAndDispositionSummaryDto> data, int timezoneOffset, DateTimeOffset dueDate, int accountingUnitId, bool isImport, bool isForeignCurrency, int divisionId)
        {
            var dueDateString = $"{dueDate:yyyy-dd-MM}";
            if (dueDate == DateTimeOffset.MaxValue)
                dueDateString = "-";

            var company = "PT DAN LIRIS";
            var title = "LAPORAN REKAP DATA HUTANG & DISPOSISI LOKAL";
            var unitName = "SEMUA UNIT";
            var date = $"JATUH TEMPO S.D. {dueDateString}";

            if (accountingUnitId > 0)
            {
                var datum = data.FirstOrDefault();
                if (datum != null)
                    unitName = $"UNIT {datum.AccountingUnitName}";
            }

            if (divisionId > 0)
            {
                var datum = data.FirstOrDefault();
                if (datum != null)
                    unitName = $"DIVISI {datum.DivisionName} {unitName}";
            }

            if (isForeignCurrency && !isImport)
                title = "LAPORAN REKAP DATA HUTANG & DISPOSISI LOKAL VALAS";

            if (isImport)
                title = "LAPORAN REKAP DATA HUTANG & DISPOSISI IMPORT";

            var categoryDataTable = GetCategoryDataTable(data);

            const int headerRow = 1;
            const int startingRow = 6;
            const int tableGap = 3;
            const int columnA = 1;
            const int columnB = 2;
            const int columnC = 3;
            const int columnD = 4;
            const int columnE = 5;

            if (!isImport && !isForeignCurrency)
            {
                var unitDataTable = GetUnitDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:E1"].Merge = true;
                    worksheet.Cells["A1:E1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:E1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:E2"].Merge = true;
                    worksheet.Cells["A2:E2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:E2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:E3"].Merge = true;
                    worksheet.Cells["A3:E3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:E3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:E4"].Merge = true;
                    worksheet.Cells["A4:E4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:E4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnE].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap}"].LoadFromDataTable(unitDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
            else
            {
                var unitCurrencyDataTable = GetUnitCurrencyDataTable(data);
                var separatedUnitCurrencyDataTable = GetSeparatedUnitCurrencyDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:E1"].Merge = true;
                    worksheet.Cells["A1:E1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:E1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:E2"].Merge = true;
                    worksheet.Cells["A2:E2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:E2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:E3"].Merge = true;
                    worksheet.Cells["A3:E3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:E3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:E4"].Merge = true;
                    worksheet.Cells["A4:E4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:E4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnE].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + tableGap + categoryDataTable.Rows.Count}"].LoadFromDataTable(unitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnB, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap}"].LoadFromDataTable(separatedUnitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnD, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
        }
        
        [HttpGet("download-pdf")]
        public IActionResult DownloadPdf([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = DebtAndDispositionSummaryPDFTemplate.Generate(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault(), accountingUnitId, isImport, isForeignCurrency, divisionId);

                var filename = "Laporan Rekap Hutang & Disposisi Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Rekap Hutang & Disposisi Lokal Valas";
                else if (isImport)
                    filename = "Laporan Rekap Hutang & Disposisi Import";
                filename += ".pdf";

                return new FileStreamResult(stream, "application/pdf")
                {
                    FileDownloadName = filename
                };
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }

        [HttpGet("debt")]
        public IActionResult GetDebt([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue;
                var result = _service.GetReportDebt(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);
                return Ok(new
                {
                    apiVersion = ApiVersion,
                    statusCode = General.OK_STATUS_CODE,
                    message = General.OK_MESSAGE,
                    data = result,
                    info = new Dictionary<string, object>
                    {
                        { "page", 1 },
                        { "size", 10 }
                    },
                });
            }
            catch (Exception e)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, e.Message + " " + e.StackTrace);
            }
        }

        [HttpGet("debt/download-excel")]
        public IActionResult DownloadExcelDebt([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = GenerateExcelDebt(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault(), accountingUnitId, isImport, isForeignCurrency);

                var filename = "Laporan Saldo Hutang (Rekap) Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Saldo Hutang (Rekap) Lokal Valas";
                else if (isImport)
                    filename = "Laporan Saldo Hutang (Rekap) Impor";
                filename += ".xlsx";

                var bytes = stream.ToArray();

                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }

        private MemoryStream GenerateExcelDebt(List<DebtAndDispositionSummaryDto> data, int timezoneOffset, DateTimeOffset dueDate, int accountingUnitId, bool isImport, bool isForeignCurrency)
        {
            var company = "PT DAN LIRIS";
            var title = "LAPORAN SALDO HUTANG USAHA (REKAP) LOKAL";
            var unitName = "SEMUA UNIT";
            var date = $"JATUH TEMPO S.D. {dueDate:yyyy-dd-MM}";

            if (accountingUnitId > 0)
            {
                var datum = data.FirstOrDefault();
                if (datum != null)
                    unitName = datum.UnitName;

            }

            if (isForeignCurrency && !isImport)
                title = "LAPORAN SALDO HUTANG USAHA (REKAP) LOKAL VALAS";

            if (isImport)
                title = "LAPORAN SALDO HUTANG USAHA (REKAP) IMPOR";

            var categoryDataTable = GetCategoryDataTableDebt(data);

            const int headerRow = 1;
            const int startingRow = 6;
            const int tableGap = 3;
            const int columnA = 1;
            const int columnC = 3;
            const int columnD = 4;
            const int columnE = 4;

            if (!isImport && !isForeignCurrency)
            {
                var unitDataTable = GetUnitDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:E1"].Merge = true;
                    worksheet.Cells["A1:E1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:E1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:E2"].Merge = true;
                    worksheet.Cells["A2:E2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:E2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:E3"].Merge = true;
                    worksheet.Cells["A3:E3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:E3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:E4"].Merge = true;
                    worksheet.Cells["A4:E4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:E4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnE].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap}"].LoadFromDataTable(unitDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
            else
            {
                var unitCurrencyDataTable = GetUnitCurrencyDataTable(data);
                var separatedUnitCurrencyDataTable = GetSeparatedUnitCurrencyDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:E1"].Merge = true;
                    worksheet.Cells["A1:E1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:E1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:E2"].Merge = true;
                    worksheet.Cells["A2:E2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:E2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:E3"].Merge = true;
                    worksheet.Cells["A3:E3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:E3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:E4"].Merge = true;
                    worksheet.Cells["A4:E4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:E4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnE].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + tableGap + categoryDataTable.Rows.Count}"].LoadFromDataTable(unitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnC].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap}"].LoadFromDataTable(separatedUnitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnD, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnE].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnD].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
        }

        private DataTable GetSeparatedUnitCurrencyDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var accountingUnits = data.Select(element => element.AccountingUnitName).Distinct().ToList();

            var debtData = data.Where(element => element.DispositionTotal == 0);
            var dispositionData = data.Where(element => element.DebtTotal == 0);

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Unit", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = " ", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });

            foreach (var accountingUnit in accountingUnits)
            {
                table.Rows.Add(accountingUnit, "", "", "");

                var currencyDebtData = debtData
                    .Where(element => element.AccountingUnitName == accountingUnit)
                    .GroupBy(element => element.CurrencyCode)
                    .Select(element => new DebtAndDispositionSummaryDto()
                    {
                        CurrencyCode = element.Key,
                        DebtTotal = element.Sum(sum => sum.DebtTotal),
                    })
                    .ToList();

                var currencyDispositionData = dispositionData
                    .Where(element => element.AccountingUnitName == accountingUnit)
                    .GroupBy(element => element.CurrencyCode)
                    .Select(element => new DebtAndDispositionSummaryDto()
                    {
                        CurrencyCode = element.Key,
                        DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                    })
                    .ToList();

                table.Rows.Add("", "Hutang", "", "");
                foreach (var currencyDebt in currencyDebtData)
                {
                    table.Rows.Add("", "", currencyDebt.CurrencyCode, currencyDebt.DebtTotal.ToString("#,##0.00"));
                }

                table.Rows.Add("", "Disposisi", "", "");
                foreach (var currencyDisposition in currencyDispositionData)
                {
                    table.Rows.Add("", "", currencyDisposition.CurrencyCode, currencyDisposition.DispositionTotal.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetSeparatedUnitCurrencyDispositionDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var accountingUnits = data.Select(element => element.AccountingUnitName).Distinct().ToList();

            var debtData = data.Where(element => element.DispositionTotal == 0);
            var dispositionData = data.Where(element => element.DebtTotal == 0);

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Unit", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });

            foreach (var accountingUnit in accountingUnits)
            {
                table.Rows.Add(accountingUnit, "", "");

                var currencyDispositionData = dispositionData
                    .Where(element => element.AccountingUnitName == accountingUnit)
                    .GroupBy(element => element.CurrencyCode)
                    .Select(element => new DebtAndDispositionSummaryDto()
                    {
                        CurrencyCode = element.Key,
                        DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                    })
                    .ToList();

                foreach (var currencyDisposition in currencyDispositionData)
                {
                    table.Rows.Add("", currencyDisposition.CurrencyCode, currencyDisposition.DispositionTotal.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetUnitCurrencyDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var accountingUnits = data.Select(element => element.AccountingUnitName).Distinct().ToList();

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });

            if (data.Count > 0)
            {
                //foreach (var unit in units)
                //{
                //    var currencyData = data
                //        .Where(element => element.UnitName == unit)
                //        .GroupBy(element => element.CurrencyCode)
                //        .Select(element => new DebtAndDispositionSummaryDto()
                //        {
                //            CurrencyCode = element.Key,
                //            DebtTotal = element.Sum(sum => sum.DebtTotal),
                //            DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                //            Total = element.Sum(sum => sum.DebtTotal) + element.Sum(sum => sum.DispositionTotal)
                //        })
                //        .ToList();

                //    table.Rows.Add(unit, "", "");

                //    foreach (var currency in currencyData)
                //    {
                //        table.Rows.Add("", currency.CurrencyCode, currency.Total.ToString("#,##0.00"));
                //    }
                //}
                var currencyData = data
                        .GroupBy(element => element.CurrencyCode)
                        .Select(element => new DebtAndDispositionSummaryDto()
                        {
                            CurrencyCode = element.Key,
                            DebtTotal = element.Sum(sum => sum.DebtTotal),
                            DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                            Total = element.Sum(sum => sum.DebtTotal) + element.Sum(sum => sum.DispositionTotal)
                        })
                        .ToList();

                foreach (var currency in currencyData)
                {
                    table.Rows.Add(currency.CurrencyCode, currency.Total.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetUnitDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var accountingUnits = data.Select(element => element.AccountingUnitName).Distinct().ToList();


            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Unit", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = " ", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total (IDR)", DataType = typeof(string) });

            if (accountingUnits.Count > 0)
            {
                foreach (var accountingUnit in accountingUnits)
                {
                    var debtTotal = data.Where(element => element.AccountingUnitName == accountingUnit).Sum(sum => sum.DebtTotal);
                    var dispositionTotal = data.Where(element => element.AccountingUnitName == accountingUnit).Sum(sum => sum.DispositionTotal);

                    table.Rows.Add(accountingUnit, "", "");
                    table.Rows.Add("", "HUTANG", debtTotal.ToString("#,##0.00"));
                    table.Rows.Add("", "DISPOSISI", dispositionTotal.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetUnitDispositionDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var accountingUnits = data.Select(element => element.AccountingUnitName).Distinct().ToList();


            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Unit", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total (IDR)", DataType = typeof(string) });

            if (accountingUnits.Count > 0)
            {
                foreach (var accountingUnit in accountingUnits)
                {
                    var dispositionTotal = data.Where(element => element.AccountingUnitName == accountingUnit).Sum(sum => sum.DispositionTotal);

                    table.Rows.Add(accountingUnit, dispositionTotal.ToString("#,##0.00"));
                }
            }

            return table;
        }

        [HttpGet("debt/download-pdf")]
        public IActionResult DownloadPdfDebt([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = DebtSummaryPDFTemplate.Generate(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault(), accountingUnitId, isImport, isForeignCurrency);

                var filename = "Laporan Saldo Hutang (Rekap) Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Saldo Hutang (Rekap) Lokal Valas";
                else if (isImport)
                    filename = "Laporan Saldo Hutang (Rekap) Impor";
                filename += ".pdf";

                return new FileStreamResult(stream, "application/pdf")
                {
                    FileDownloadName = filename
                };
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }

        [HttpGet("dispositions")]
        public IActionResult GetDisposition([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue;
                var result = _service.GetReportDisposition(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);
                return Ok(new
                {
                    apiVersion = ApiVersion,
                    statusCode = General.OK_STATUS_CODE,
                    message = General.OK_MESSAGE,
                    data = result,
                    info = new Dictionary<string, object>
                    {
                        { "page", 1 },
                        { "size", 10 }
                    },
                });
            }
            catch (Exception e)
            {
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, e.Message + " " + e.StackTrace);
            }
        }

        [HttpGet("dispositions/download-excel")]
        public IActionResult DownloadExcelDisposition([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetDispositionSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = GenerateExcelDisposition(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault().AddHours(_identityService.TimezoneOffset), accountingUnitId, isImport, isForeignCurrency, divisionId);

                var filename = "Laporan Rekap Disposisi Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Rekap Disposisi Lokal Valas";
                else if (isImport)
                    filename = "Laporan Rekap Disposisi Import";
                filename += ".xlsx";

                var bytes = stream.ToArray();

                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }

        private MemoryStream GenerateExcelDisposition(List<DebtAndDispositionSummaryDto> data, int timezoneOffset, DateTimeOffset dueDate, int accountingUnitId, bool isImport, bool isForeignCurrency, int divisionId)
        {
            var dueDateString = $"{dueDate:yyyy-dd-MM}";
            if (dueDate == DateTimeOffset.MaxValue)
                dueDateString = "-";

            var company = "PT DAN LIRIS";
            var title = "LAPORAN REKAP DATA DISPOSISI LOKAL";
            var unitName = "SEMUA UNIT";
            var date = $"JATUH TEMPO S.D. {dueDateString}";

            if (accountingUnitId > 0)
            {
                var datum = data.FirstOrDefault();
                if (datum != null)
                    unitName = datum.UnitName;
            }

            if (divisionId > 0)
            {
                var datum = data.FirstOrDefault();
                if (datum != null)
                    unitName = $"DIVISI {datum.DivisionName} {unitName}";
            }

            if (isForeignCurrency && !isImport)
                title = "LAPORAN REKAP DATA DISPOSISI LOKAL VALAS";

            if (isImport)
                title = "LAPORAN REKAP DATA DISPOSISI IMPORT";

            var categoryDataTable = GetCategoryDispositionDataTable(data);

            const int headerRow = 1;
            const int startingRow = 6;
            const int tableGap = 3;
            const int columnA = 1;
            const int columnB = 2;
            const int columnC = 3;
            const int columnD = 4;
            const int columnE = 5;

            if (!isImport && !isForeignCurrency)
            {
                var unitDataTable = GetUnitDispositionDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:C1"].Merge = true;
                    worksheet.Cells["A1:C1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:C1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:C2"].Merge = true;
                    worksheet.Cells["A2:C2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:C2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:C3"].Merge = true;
                    worksheet.Cells["A3:C3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:C3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:C4"].Merge = true;
                    worksheet.Cells["A4:C4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:C4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnE].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap}"].LoadFromDataTable(unitDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnB, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnB].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnB].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnB].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnB].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitDataTable.Rows.Count, columnB].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
            else
            {
                var unitCurrencyDataTable = GetUnitCurrencyDataTable(data);
                var separatedUnitCurrencyDataTable = GetSeparatedUnitCurrencyDispositionDataTable(data);

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Sheet 1");
                    worksheet.Cells["A1"].Value = company;
                    worksheet.Cells["A1:C1"].Merge = true;
                    worksheet.Cells["A1:C1"].Style.Font.Size = 20;
                    worksheet.Cells["A1:C1"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Value = title;
                    worksheet.Cells["A2:C2"].Merge = true;
                    worksheet.Cells["A2:C2"].Style.Font.Size = 20;
                    worksheet.Cells["A2:C2"].Style.Font.Bold = true;
                    worksheet.Cells["A3"].Value = unitName;
                    worksheet.Cells["A3:C3"].Merge = true;
                    worksheet.Cells["A3:C3"].Style.Font.Size = 20;
                    worksheet.Cells["A3:C3"].Style.Font.Bold = true;
                    worksheet.Cells["A4"].Value = date;
                    worksheet.Cells["A4:C4"].Merge = true;
                    worksheet.Cells["A4:C4"].Style.Font.Size = 20;
                    worksheet.Cells["A4:C4"].Style.Font.Bold = true;
                    worksheet.Cells["A6"].LoadFromDataTable(categoryDataTable, true);
                    worksheet.Cells[startingRow + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count, columnC].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + tableGap + categoryDataTable.Rows.Count}"].LoadFromDataTable(unitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnB, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[$"A{startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap}"].LoadFromDataTable(separatedUnitCurrencyDataTable, true);
                    worksheet.Cells[startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnC, startingRow + headerRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnC].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
                    worksheet.Cells[worksheet.Cells.Address].AutoFitColumns();

                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow, columnA, startingRow + categoryDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count, columnB].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnC].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnC].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnC].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow, columnA, startingRow + categoryDataTable.Rows.Count + tableGap + headerRow + unitCurrencyDataTable.Rows.Count + tableGap + headerRow + separatedUnitCurrencyDataTable.Rows.Count, columnC].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                    var stream = new MemoryStream();
                    package.SaveAs(stream);

                    return stream;
                }
            }
        }

        [HttpGet("dispositions/download-pdf")]
        public IActionResult DownloadPdfDisposition([FromQuery] int categoryId, [FromQuery] int accountingUnitId, [FromQuery] int divisionId, [FromQuery] DateTimeOffset? dueDate, [FromQuery] bool isImport, [FromQuery] bool isForeignCurrency)
        {

            try
            {
                if (!dueDate.HasValue)
                    dueDate = DateTimeOffset.MaxValue.AddHours(Math.Abs(_identityService.TimezoneOffset) * -1);

                var result = _service.GetDispositionSummary(categoryId, accountingUnitId, divisionId, dueDate.GetValueOrDefault(), isImport, isForeignCurrency);

                var stream = DebtAndDispositionSummaryPDFTemplate.GenerateDisposition(result, _identityService.TimezoneOffset, dueDate.GetValueOrDefault(), accountingUnitId, isImport, isForeignCurrency);

                var filename = "Laporan Rekap Disposisi Lokal";
                if (isForeignCurrency)
                    filename = "Laporan Rekap Disposisi Lokal Valas";
                else if (isImport)
                    filename = "Laporan Rekap Disposisi Import";
                filename += ".pdf";

                return new FileStreamResult(stream, "application/pdf")
                {
                    FileDownloadName = filename
                };
            }
            catch (Exception e)
            {
                var result = new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message).Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, result);
            }
        }

        private DataTable GetCategoryDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var categoryData = data
               .GroupBy(element => new { element.CategoryCode, element.CurrencyCode })
               .Select(element => new DebtAndDispositionSummaryDto()
               {
                   CategoryCode = element.Key.CategoryCode,
                   CategoryName = element.FirstOrDefault().CategoryName,
                   CurrencyCode = element.Key.CurrencyCode,
                   DebtTotal = element.Sum(sum => sum.DebtTotal),
                   DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                   Total = element.Sum(sum => sum.DebtTotal) + element.Sum(sum => sum.DispositionTotal)
               })
               .ToList();

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Kategori", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Hutang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Disposisi", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });

            if (categoryData.Count > 0)
            {
                foreach (var categoryDatum in categoryData)
                {
                    table.Rows.Add(categoryDatum.CategoryName, categoryDatum.CurrencyCode, categoryDatum.DebtTotal.ToString("#,##0.00"), categoryDatum.DispositionTotal.ToString("#,##0.00"), categoryDatum.Total.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetCategoryDispositionDataTable(List<DebtAndDispositionSummaryDto> data)
        {
            var categoryData = data
               .GroupBy(element => new { element.CategoryCode, element.CurrencyCode })
               .Select(element => new DebtAndDispositionSummaryDto()
               {
                   CategoryCode = element.Key.CategoryCode,
                   CategoryName = element.FirstOrDefault().CategoryName,
                   CurrencyCode = element.Key.CurrencyCode,
                   DebtTotal = element.Sum(sum => sum.DebtTotal),
                   DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                   Total = element.Sum(sum => sum.DebtTotal) + element.Sum(sum => sum.DispositionTotal)
               })
               .ToList();

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Kategori", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Disposisi", DataType = typeof(string) });

            if (categoryData.Count > 0)
            {
                foreach (var categoryDatum in categoryData)
                {
                    table.Rows.Add(categoryDatum.CategoryName, categoryDatum.CurrencyCode, categoryDatum.DispositionTotal.ToString("#,##0.00"));
                }
            }

            return table;
        }

        private DataTable GetCategoryDataTableDebt(List<DebtAndDispositionSummaryDto> data)
        {
            var categoryData = data
               .GroupBy(element => new { element.CategoryCode, element.CurrencyCode })
               .Select(element => new DebtAndDispositionSummaryDto()
               {
                   CategoryCode = element.Key.CategoryCode,
                   CategoryName = element.FirstOrDefault().CategoryName,
                   CurrencyCode = element.Key.CurrencyCode,
                   DebtTotal = element.Sum(sum => sum.DebtTotal),
                   //DispositionTotal = element.Sum(sum => sum.DispositionTotal),
                   Total = element.Sum(sum => sum.DebtTotal) + element.Sum(sum => sum.DispositionTotal)
               })
               .ToList();

            var table = new DataTable();

            table.Columns.Add(new DataColumn() { ColumnName = "Kategori", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Mata Uang", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Hutang", DataType = typeof(string) });
            //table.Columns.Add(new DataColumn() { ColumnName = "Disposisi", DataType = typeof(string) });
            table.Columns.Add(new DataColumn() { ColumnName = "Total", DataType = typeof(string) });

            if (categoryData.Count > 0)
            {
                foreach (var categoryDatum in categoryData)
                {
                    table.Rows.Add(categoryDatum.CategoryName, categoryDatum.CurrencyCode, categoryDatum.DebtTotal.ToString("#,##0.00"), categoryDatum.Total.ToString("#,##0.00"));
                }
            }

            return table;
        }

    }
}
