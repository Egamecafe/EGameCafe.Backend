﻿using AutoMapper;
using EGameCafe.Application.Common.Mappings;
using EGameCafe.Domain.Entities;
using EGameCafe.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGameCafe.Application.Groups.Queries.GetAllGroups
{
    public class GetAllGroupsDto : IMapFrom<GetAllGroupsDto>
    {
        public string GroupId { get; set; }
        public string GroupName { get; set; }
        public GroupType GroupType { get; set; }

        public void Mapping(Profile profile)
        {
            profile.CreateMap<Group, GetAllGroupsDto>();
        }
    }
}