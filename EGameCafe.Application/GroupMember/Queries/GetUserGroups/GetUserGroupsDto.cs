﻿using AutoMapper;
using EGameCafe.Application.Common.Mappings;
using EGameCafe.Domain.Entities;
using EGameCafe.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGameCafe.Application.GroupMembers.Queries.GetUserGroups
{
    public class GetUserGroupsDto  : IMapFrom<GetUserGroupsDto>
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public GroupType GroupType { get; set; }
        public void Mapping(Profile profile)
        {
            profile.CreateMap<Group, GetUserGroupsDto>();
        }
    }
}
