﻿using EGameCafe.Domain.Entities;
using EGameCafe.Domain.Enums;
using EGameCafe.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGameCafe.Infrastructure.Persistence
{
    public class ApplicationDbContextSeed
    {
        public static async Task<string> SeedDefaultUserAsync(UserManager<ApplicationUser> userManager, string username, string email)
        {
            var user = new ApplicationUser()
            {
                Email = email,
                FirstName = "mohammad",
                LastName = "Talachi",
                UserName = username,
                PhoneNumber = "0933333333",
                BirthDate = new DateTime(1999, 11, 24)
            };

            await userManager.CreateAsync(user, "password");

            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);

            await userManager.ConfirmEmailAsync(user, token);

            return user.Id;
        }

        public static async Task SeedSampleDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {

            var userid_1 = await SeedDefaultUserAsync(userManager, "Test_1", "test1@test.com");

            var userid_2 = await SeedDefaultUserAsync(userManager, "Test_2", "test2@test.com");

            // Seed, if necessary
            if (!context.Group.Any())
            {
                var item = new Group
                {
                    GroupId = Guid.NewGuid().ToString(),
                    GroupName = "gpTest",
                    GroupType = GroupType.publicGroup,
                    SharingLink = "DB4C76CF1EE2B97DF9E180314C74F22384C06E82"
                };

                context.Group.Add(item);

                await context.SaveChangesAsync();
            }

            if (!context.GroupMember.Any())
            {
                var items = new List<GroupMember>
                {
                 new GroupMember
                {
                    GroupMemberId = Guid.NewGuid().ToString(),
                    IsBlock = false,
                    GroupId = "E17E1211E14BC57544F83D9FD53CD769D2AE0E18A5ABE149A2278C9E0FFE9D03",
                    UserId =  userid_1
                },

                new GroupMember
                {
                    GroupMemberId = Guid.NewGuid().ToString(),
                    IsBlock = false,
                    GroupId = Guid.NewGuid().ToString(),
                    UserId = userid_2
                }
            };

                await context.GroupMember.AddRangeAsync(items);

                await context.SaveChangesAsync();
            }

        }
    }
}
