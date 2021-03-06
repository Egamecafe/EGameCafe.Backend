﻿using EGameCafe.Application.Common.Interfaces;
using EGameCafe.Domain.Entities;
using EGameCafe.Domain.Enums;
using EGameCafe.Infrastructure.Identity;
using EGameCafe.Infrastructure.Persistence;
using EGameCafe.Server;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Respawn;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Application.IntegrationTests
{
    [SetUpFixture]
    public class Testing
    {
        private static IConfigurationRoot _configuration;
        private static IServiceScopeFactory _scopeFactory;
        private static Checkpoint _checkpoint;
        private static string _currentUserId;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddEnvironmentVariables();

            _configuration = builder.Build();

            var startup = new Startup(_configuration);

            var services = new ServiceCollection();

            services.AddSingleton(Mock.Of<IWebHostEnvironment>(w =>
                w.EnvironmentName == "Development" &&
                w.ApplicationName == "EGameCafe.Server"));

            services.AddLogging();

            startup.ConfigureServices(services);

            // Replace service registration for ICurrentUserService
            // Remove existing registration
            var currentUserServiceDescriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(ICurrentUserService));

            services.Remove(currentUserServiceDescriptor);

            // Register testing version
            services.AddTransient(provider =>
                Mock.Of<ICurrentUserService>(s => s.UserId == _currentUserId));

            _scopeFactory = services.BuildServiceProvider().GetService<IServiceScopeFactory>();

            _checkpoint = new Checkpoint
            {
                TablesToIgnore = new[] { "__EFMigrationsHistory" }
            };

            EnsureDatabase();
        }

        private static void EnsureDatabase()
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();

            context.Database.Migrate();
        }

        public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        {
            using var scope = _scopeFactory.CreateScope();

            var mediator = scope.ServiceProvider.GetService<IMediator>();

            return await mediator.Send(request);
        }

        public static async Task<string> RunAsDefaultUserAsync()
        {
            return await RunAsUserAsync("mohi", "Testing1234!");
        }

        public static async Task<string> RunAsUserAsync(string userName, string password)
        {
            using var scope = _scopeFactory.CreateScope();

            var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser { UserName = userName, Email = "test@test.com", FirstName = "mohammad", LastName = "talachi", BirthDate = new DateTime(1999, 11, 24) };

            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await AddUserDetail(user.Id);

                _currentUserId = user.Id;

                return _currentUserId;
            }

            var errors = string.Join(Environment.NewLine, result.ToApplicationResult());

            throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
        }

        public static async Task AddUserDetail(string userId)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();

            var userDetail = new UserDetail { UserId = userId,  };

            await context.UserDetails.AddAsync(userDetail);

            await context.SaveChangesAsync();
        }

        public static async Task CreateRandomGroup(GroupType groupType)
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();

            var group = new Group { 
                GroupId = Guid.NewGuid().ToString(),
                GroupName = GenerateRandomString(5),
                SharingLink = await GenerateSHA1Hash(),
                GroupType = groupType
            };

            await context.Group.AddAsync(group);

            await context.SaveChangesAsync();
        }

        public static async Task<string> GenerateSHA1Hash()
        {
            using var scope = _scopeFactory.CreateScope();

            var generator = scope.ServiceProvider.GetService<IIdGenerator>();

            return await generator.SHA1hashGenerator(GenerateRandomString(5));
        }

        public static IDateTime GetDateTime()
        {
            using var scope = _scopeFactory.CreateScope();

            var datetime = scope.ServiceProvider.GetService<IDateTime>();

            return datetime;
        }

        public static async Task ResetState()
        {
            await _checkpoint.Reset(_configuration.GetConnectionString("DefaultConnection"));
            _currentUserId = null;
        }

        public static void ResetCache()
        {
            using var scope = _scopeFactory.CreateScope();

            var _memoryCache = scope.ServiceProvider.GetService<IMemoryCache>();

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        public static async Task<TEntity> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();

            return await context.FindAsync<TEntity>(keyValues);
        }

        public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
        {
            using var scope = _scopeFactory.CreateScope();

            var context = scope.ServiceProvider.GetService<ApplicationDbContext>();

            context.Add(entity);

            await context.SaveChangesAsync();
        }

        public static string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            Random random = new Random();

            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [OneTimeTearDown]
        public void RunAfterAnyTests()
        {
        }
    }
}
