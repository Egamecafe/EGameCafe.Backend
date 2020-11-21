﻿using EGameCafe.Application.Common.Exceptions;
using EGameCafe.Application.GroupMember.Commands.KickUser;
using EGameCafe.Domain.Entities;
using EGameCafe.Domain.Enums;
using FluentAssertions;
using NUnit.Framework;
using System.Threading.Tasks;


namespace Application.IntegrationTests.GroupMember.Commands
{
    using static Testing;

    public class JoinGroupTests : TestBase
    {
        [Test]
        public void ShouldRequireMinimumFields()
        {
            var command = new KickUserCommand();

            FluentActions.Invoking(() =>
                SendAsync(command)).Should().Throw<ValidationException>();
        }

        [Test]
        public async Task ShouldKickFromGroupAndReturnSucceeded()
        {

            var userId = await RunAsDefaultUserAsync();

            var groupId = await GenerateRandomId();

            var sharingLink = await GenerateSHA1Hash();

            var item = new GamingGroups
            {
                GamingGroupGroupId = groupId,
                GroupName = "gpTest",
                GroupType = GroupType.publicGroup,
                SharingLink = sharingLink,
            };

            await AddAsync(item);


            var groupMemberId = await GenerateRandomId();

            var groupMember = new GamingGroupMembers
            {
                GroupId = groupId,
                GroupMemberId = groupMemberId,
                UserId = userId
            };

            await AddAsync(groupMember);

            var command = new KickUserCommand
            {
                GroupId = groupId,
                UserId = userId
            };

            var result = await SendAsync(command);

            result.Should().NotBeNull();
            result.Succeeded.Should().BeTrue();
            result.Status.Should().Equals(200);

        }

        [Test]
        public async Task ShouldKickFromGroup()
        {
            var userId = await RunAsDefaultUserAsync();

            var groupId = await GenerateRandomId();

            var sharingLink = await GenerateSHA1Hash();

            var item = new GamingGroups
            {
                GamingGroupGroupId = groupId,
                GroupName = "gpTest",
                GroupType = GroupType.publicGroup,
                SharingLink = sharingLink,
            };

            await AddAsync(item);

            var groupMemberId = await GenerateRandomId();

            var groupMember = new GamingGroupMembers
            {
                GroupId = groupId,
                GroupMemberId = groupMemberId,
                UserId = userId
            };

            await AddAsync(groupMember);

            var command = new KickUserCommand
            {
                GroupId = groupId,
                UserId = userId
            };

            var result = await SendAsync(command);

            var gamingGroup = await FindAsync<GamingGroupMembers>(result.Id);

            gamingGroup.Should().NotBeNull();
            gamingGroup.GroupId.Should().Be(groupId);
            gamingGroup.UserId.Should().Be(userId);
            gamingGroup.Block.Should().Be(true);
            //gamingGroup..Should().Be(userId);
            //gamingGroup.Created.Should().BeCloseTo(DateTime.Now, 10000);
        }

        [Test]
        public async Task ShouldNotFindUser()
        {
            var groupId = await GenerateRandomId();
            var userId = await GenerateRandomId();

            var command = new KickUserCommand
            {
                GroupId = groupId,
                UserId = userId
            };

            FluentActions.Invoking(() =>
                SendAsync(command)).Should().Throw<NotFoundException>();

        }

    }
}
