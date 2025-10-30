using System;
using AutoMapper;
using Domain;
using Application.Activities;

namespace Application.Core;

public class MappingProfiles: Profile
{
    public MappingProfiles()
    {
        CreateMap<Activity, Activity>();
    }

}
