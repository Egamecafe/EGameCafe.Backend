﻿using EGameCafe.Application.Common.Exceptions;
using EGameCafe.Application.Common.Interfaces;
using EGameCafe.Application.Common.Models;
using EGameCafe.Domain.Entities;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EGameCafe.Application.GroupMembers.Commands.JoinGroup
{
    public class JoinGroupCommand : IRequest<Result>
    {
        public string UserId { get; set; }
        public string GroupId { get; set; }
    }

    public class Handler : IRequestHandler<JoinGroupCommand, Result>
    {
        private readonly IApplicationDbContext _context;

        public Handler(IApplicationDbContext context)
        {
            _context = context;

        }

        public async Task<Result> Handle(JoinGroupCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if(_context.GroupMember.Where(e=>e.GroupId == request.GroupId && e.UserId == request.UserId).Any())
                {
                    throw new DuplicateUserException($"DuplicateUser : {request.UserId} groupId : {request.GroupId}");
                }

                var entry = new GroupMember()
                {
                    GroupId = request.GroupId,
                    UserId = request.UserId
                };

                entry.GroupMemberId = Guid.NewGuid().ToString();

                _context.GroupMember.Add(entry);

                await _context.SaveChangesAsync(cancellationToken);

                return Result.Success(entry.GroupMemberId, "https://tools.ietf.org/html/rfc7231#section-6.3.1", 201, "Created");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
