using Microsoft.Extensions.DependencyInjection;
using SelfTracker.Application.Abstractions;
using SelfTracker.Application.Features;

namespace SelfTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<PlanContext>();
        services.AddScoped<EntityLookup>();
        services.AddScoped<DailyLogService>();
        services.AddScoped<FileUploadService>();
        services.AddScoped<Purger>();
        services.AddScoped<DsaAttemptService>();
        services.AddScoped<DerivedTime>();
        services.AddScoped<DdiaTaskResolver>();
        services.AddScoped<PlanTaskEnricher>();
        services.AddScoped<PlanTaskService>();
        services.AddScoped<ExitCriterionService>();
        services.AddScoped<CheckpointService>();
        services.AddScoped<WeeklyHoursService>();
        services.AddScoped<WeeklyReviewService>();
        services.AddScoped<MonthlyReviewService>();
        services.AddScoped<SomedayService>();
        services.AddScoped<NudgeService>();
        services.AddScoped<ReadingLogService>();
        services.AddScoped<DdiaNoteService>();
        services.AddScoped<DesignDrillService>();
        services.AddScoped<ArticulationService>();

        // Register every IHandler<,> implementation in this assembly.
        var handlerInterface = typeof(IHandler<,>);
        foreach (var type in typeof(DependencyInjection).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
            {
                services.AddScoped(iface, type);
            }
        }

        AddCrud<PauseRangeSpec, Domain.Entities.PauseRange, PauseRangeDto, PauseRangeInput>(services);
        AddCrud<TravelOverrideSpec, Domain.Entities.TravelOverride, TravelOverrideDto, TravelOverrideInput>(services);
        AddCrud<ScheduleBlockSpec, Domain.Entities.ScheduleBlock, ScheduleBlockDto, ScheduleBlockInput>(services);
        AddCrud<GymPlanDaySpec, Domain.Entities.GymPlanDay, GymPlanDayDto, GymPlanDayInput>(services);
        AddCrud<DsaTopicSpec, Domain.Entities.DsaTopic, DsaTopicDto, DsaTopicInput>(services);
        AddCrud<DsaProblemSpec, Domain.Entities.DsaProblem, DsaProblemDto, DsaProblemInput>(services);
        AddCrud<DdiaChapterSpec, Domain.Entities.DdiaChapter, DdiaChapterDto, DdiaChapterInput>(services);
        AddCrud<ArticulationPromptSpec, Domain.Entities.ArticulationPrompt, ArticulationPromptDto, ArticulationPromptInput>(services);
        AddCrud<ReplanSpec, Domain.Entities.ReplanEvent, ReplanDto, ReplanInput>(services);
        AddCrud<SomedaySpec, Domain.Entities.SomedayItem, SomedayDto, SomedayInput>(services);
        return services;
    }

    private static void AddCrud<TSpec, TEntity, TDto, TInput>(IServiceCollection services)
        where TSpec : class, ICrudSpec<TEntity, TDto, TInput>
        where TEntity : Domain.Common.Entity, new()
        where TInput : IVersioned
    {
        services.AddScoped<ICrudSpec<TEntity, TDto, TInput>, TSpec>();
        services.AddScoped<CrudService<TEntity, TDto, TInput>>();
    }
}
