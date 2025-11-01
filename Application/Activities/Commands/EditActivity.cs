using System;
using Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistent;
using AutoMapper;

namespace Application.Activities.Commands;

public class EditActivity
{
    public class Command : IRequest<string>
    {
        public required Activity Activity { get; set; }
    }
    
    public class Handler(AppDbContext context, IMapper mapper) : IRequestHandler<Command, string>
    {
        public async Task<string> Handle(Command request, CancellationToken cancellationToken)
        {
            var activity = await context.Activities
                         .FindAsync([request.Activity.Id], cancellationToken)
                             ?? throw new Exception("Could not find activity");

            mapper.Map(request.Activity, activity);

            await context.SaveChangesAsync(cancellationToken);

            return request.Activity.Id;
        }
    }
}