﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DFACore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using DFACore.Repository;
using Newtonsoft.Json;
using Wkhtmltopdf.NetCore;
using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace DFACore.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicantRecordRepository _applicantRepo;
        private readonly IMessageService _messageService;
        private readonly IGeneratePdf _generatePdf;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicantRecordRepository applicantRepo,
            IMessageService messageService,
            IGeneratePdf generatePdf,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _applicantRepo = applicantRepo;
            _messageService = messageService;
            _generatePdf = generatePdf;
            _env = env;
        }

        public IActionResult Index()
        {
            var stringify = JsonConvert.SerializeObject(_applicantRepo.GenerateListOfDates(DateTime.Now));
            ViewData["AvailableDates"] = stringify;
            ViewData["ApplicationCode"] = GetApplicantCode();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ApplicantRecordViewModel record, string returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            var applicantRecord = new ApplicantRecord
            {
                Title = record.Title,
                FirstName = record.FirstName,
                MiddleName = record.MiddleName,
                LastName = record.LastName,
                Suffix = record.Suffix,
                Address = record.Address,
                Nationality = record.Nationality,
                ContactNumber = record.ContactNumber,
                CompanyName = record.CompanyName,
                CountryDestination = record.CountryDestination,
                NameOfRepresentative = record.NameOfRepresentative,
                RepresentativeContactNumber = record.RepresentativeContactNumber,
                ApostileData = record.ApostileData,
                ProcessingSite = record.ProcessingSite,
                ProcessingSiteAddress = record.ProcessingSiteAddress,
                ScheduleDate = DateTime.ParseExact(record.ScheduleDate, "MM/dd/yyyy hh:mm tt",
                                       System.Globalization.CultureInfo.InvariantCulture),
                ApplicationCode = record.ApplicationCode,
                CreatedBy = new Guid(_userManager.GetUserId(User))
            };


            var result = _applicantRepo.Add(applicantRecord);
            if (!result)
            {
                ModelState.AddModelError(string.Empty, "An error has occured while saving the data.");
            }
            //var name = await _userManager.FindByIdAsync(_userManager.GetUserId(User));

            var attachment = new Attachment("test.pdf", await GeneratePDF(), new MimeKit.ContentType("application", "pdf"));

            await _messageService.SendEmailAsync(User.Identity.Name, User.Identity.Name, "Reset Password",
                    $"Download the attachment and present to the selected branch.",
                    attachment);

            return RedirectToAction("Success");
        }


        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Success()
        {
            return View();
        }

        public static string GetApplicantCode()
        {
            int length = 4;
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[random.Next(s.Length)])
                          .ToArray());

            Random r = new Random();
            var date = DateTime.Now;
            var applicantCode = $"{date.ToString("hhmmss")}-{date.ToString("ddd").Substring(0, 2)}{result}-{date.ToString("MMdd")}".ToUpper();

            return applicantCode;
        }


        public async Task<MemoryStream> GeneratePDF()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + "header.html";
            var path2 = _env.WebRootFileProvider.GetFileInfo("header.html")?.PhysicalPath;
            var options = new ConvertOptions
            {
                HeaderHtml = path2,
                PageOrientation = Wkhtmltopdf.NetCore.Options.Orientation.Portrait,
            };
            _generatePdf.SetConvertOptions(options);

            var data = new TestData
            {
                Text = "This is a test",
                Number = 123456
            };

            var pdf = await _generatePdf.GetByteArray("Views/TestBootstrapSSL.cshtml", data);
            var pdfStream = new System.IO.MemoryStream();
            pdfStream.Write(pdf, 0, pdf.Length);
            pdfStream.Position = 0;
            return pdfStream;
        }

        public ActionResult ValidateScheduleDate(string scheduleDate)
        {
            var date = DateTime.ParseExact(scheduleDate, "MM/dd/yyyy hh:mm tt",
                                       System.Globalization.CultureInfo.InvariantCulture);
            var result = _applicantRepo.ValidateScheduleDate(date);

            return Json(result);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [AllowAnonymous]
        public ActionResult Test()
        {
            var result = _applicantRepo.GenerateListOfDates(DateTime.Now);//_applicantRepo.GetUnAvailableDates();
            return Json(result);
        }
    }
}
