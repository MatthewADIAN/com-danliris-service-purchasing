﻿using AutoMapper;
using Com.DanLiris.Service.Purchasing.Lib.Interfaces;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Com.DanLiris.Service.Purchasing.WebApi.Controllers.v1.GarmentReports
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/garment-stock-report")]
    [Authorize]
    public class GarmentStockReportController : Controller
    {
        private string ApiVersion = "1.0.0";
        private readonly IMapper mapper;
        private readonly IGarmentStockReportFacade _facade;
        private readonly IServiceProvider serviceProvider;
        private readonly IdentityService identityService;

        public GarmentStockReportController(IGarmentStockReportFacade facade, IServiceProvider serviceProvider)
        {
            this._facade = facade;
            this.serviceProvider = serviceProvider;
        }

        [HttpGet]
        public IActionResult GetReportGarmentStock(DateTime? dateFrom, DateTime? dateTo, string category, string unitcode, int page = 1, int size = 25, string Order = "{}")
        {
            try
            {
                int offset = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                string accept = Request.Headers["Accept"];

                var data = _facade.GetStockReport(offset, unitcode, category, page, size, Order, dateFrom, dateTo);



                return Ok(new
                {
                    apiVersion = ApiVersion,
                    data = data.Item1,
                    info = new { total = data.Item2 },
                    message = General.OK_MESSAGE,
                    statusCode = General.OK_STATUS_CODE
                });
            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

        [HttpGet("download")]
        public IActionResult GetXls(DateTime? dateFrom, DateTime? dateTo, string category, string unitcode)
        {

            try
            {
                byte[] xlsInBytes;
                int offset = Convert.ToInt32(Request.Headers["x-timezone-offset"]);
                //DateTime DateFrom = dateFrom == null ? new DateTime(1970, 1, 1) : Convert.ToDateTime(dateFrom);
                //DateTime DateTo = dateTo == null ? DateTime.Now : Convert.ToDateTime(dateTo);

                MemoryStream xls = _facade.GenerateExcelStockReport(category, unitcode, dateFrom, dateTo, offset);


                string filename = String.IsNullOrWhiteSpace(unitcode) ? String.Format("Laporan Stock Gudang All Unit - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy")) : unitcode == "C2A" ? String.Format("Laporan Stock Gudang KONFEKSI 2A - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy")) : unitcode == "C2B" ? String.Format("Laporan Stock Gudang KONFEKSI 2B - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy")) : unitcode == "C2C" ? String.Format("Laporan Stock Gudang KONFEKSI 2C - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy")) : unitcode == "C1B" ? String.Format("Laporan Stock Gudang KONFEKSI 2D - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy")) : String.Format("Laporan  All KONFEKSI 1 MNS - {0}.xlsx", DateTime.UtcNow.ToString("ddMMyyyy"));

                xlsInBytes = xls.ToArray();
                var file = File(xlsInBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
                return file;

            }
            catch (Exception e)
            {
                Dictionary<string, object> Result =
                    new ResultFormatter(ApiVersion, General.INTERNAL_ERROR_STATUS_CODE, e.Message)
                    .Fail();
                return StatusCode(General.INTERNAL_ERROR_STATUS_CODE, Result);
            }
        }

    }
}

