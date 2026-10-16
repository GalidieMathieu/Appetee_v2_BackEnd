namespace Appetee.Infrastructure.MealPlans;

internal static class NutritionPlanSql
{
    internal const string Context = """
        SELECT COALESCE(s.revision,0) Revision, s.current_nutrition_plan_id CurrentNutritionPlanId,
            u.calculation_sex CalculationSex, u.birth_year BirthYear, u.height_cm HeightCm, u.weight_kg WeightKg
        FROM users u LEFT JOIN meal_plan_user_settings s ON s.user_id=u.id WHERE u.id=@CurrentUserId;
        """;
    internal const string SummaryColumns = """
        p.id Id,p.name Name,p.mode Mode,p.default_meals_per_day DefaultMealsPerDay,
        p.goal Goal,p.activity_level ActivityLevel,p.calculation_policy_version CalculationPolicyVersion,
        p.target_calories_kcal TargetCaloriesKcal,p.target_protein_g TargetProteinG,
        p.target_carbs_g TargetCarbsG,p.target_fat_g TargetFatG,
        p.calculated_at CalculatedAt,p.created_at CreatedAt,p.updated_at UpdatedAt
        """;
    internal const string FullColumns = SummaryColumns + "," + """
        p.calculation_sex CalculationSex,p.birth_year BirthYear,p.age_years AgeYears,
        p.height_cm HeightCm,p.weight_kg WeightKg,p.rmr_kcal RmrKcal,p.maintenance_kcal MaintenanceKcal
        """;
    internal const string Current = "SELECT " + SummaryColumns + """
         FROM nutrition_plans p JOIN meal_plan_user_settings s
         ON s.user_id=p.user_id AND s.current_nutrition_plan_id=p.id WHERE p.user_id=@CurrentUserId;
        """;
    internal const string List = "SELECT " + SummaryColumns + """
         FROM nutrition_plans p LEFT JOIN meal_plan_user_settings s ON s.user_id=p.user_id
         WHERE p.user_id=@CurrentUserId
         ORDER BY (p.id=s.current_nutrition_plan_id) DESC,p.updated_at DESC,p.id DESC LIMIT 50;
        """;
    internal const string Detail = "SELECT " + FullColumns + """
         FROM nutrition_plans p WHERE p.user_id=@CurrentUserId AND p.id=@PlanId;
        """;
    internal const string Count = "SELECT COUNT(*) FROM nutrition_plans WHERE user_id=@CurrentUserId;";
    internal const string NameExists = "SELECT EXISTS(SELECT 1 FROM nutrition_plans WHERE user_id=@CurrentUserId AND name=@Name);";
    internal const string Insert = """
        INSERT INTO nutrition_plans(user_id,name,mode,default_meals_per_day,goal,activity_level,
            calculation_sex,birth_year,age_years,height_cm,weight_kg,calculation_policy_version,
            rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,
            calculated_at,created_at,updated_at)
        VALUES(@CurrentUserId,@Name,@Mode,@DefaultMealsPerDay,@Goal,@ActivityLevel,
            @CalculationSex,@BirthYear,@AgeYears,@HeightCm,@WeightKg,@PolicyVersion,
            @RmrKcal,@MaintenanceKcal,@CaloriesKcal,@ProteinG,@CarbsG,@FatG,@CalculatedAt,@TimestampUtc,@TimestampUtc);
        SELECT LAST_INSERT_ID();
        """;
    internal const string SetFirstCurrent = """
        UPDATE meal_plan_user_settings SET current_nutrition_plan_id=@PlanId
        WHERE user_id=@CurrentUserId AND current_nutrition_plan_id IS NULL;
        """;
    internal const string NameExistsOther = "SELECT EXISTS(SELECT 1 FROM nutrition_plans WHERE user_id=@CurrentUserId AND name=@Name AND id<>@PlanId);";
    internal const string CurrentId = "SELECT current_nutrition_plan_id FROM meal_plan_user_settings WHERE user_id=@CurrentUserId;";
    internal const string UpdateMetadata = """
        UPDATE nutrition_plans SET name=@Name, default_meals_per_day=@DefaultMealsPerDay, updated_at=@TimestampUtc
        WHERE user_id=@CurrentUserId AND id=@PlanId;
        """;
    internal const string UpdateCalculation = """
        UPDATE nutrition_plans SET name=@Name, default_meals_per_day=@DefaultMealsPerDay,
            goal=@Goal,activity_level=@ActivityLevel,calculation_sex=@CalculationSex,birth_year=@BirthYear,
            age_years=@AgeYears,height_cm=@HeightCm,weight_kg=@WeightKg,calculation_policy_version=@PolicyVersion,
            rmr_kcal=@RmrKcal,maintenance_kcal=@MaintenanceKcal,target_calories_kcal=@CaloriesKcal,
            target_protein_g=@ProteinG,target_carbs_g=@CarbsG,target_fat_g=@FatG,
            calculated_at=@TimestampUtc,updated_at=@TimestampUtc WHERE user_id=@CurrentUserId AND id=@PlanId;
        """;
    internal const string Duplicate = """
        INSERT INTO nutrition_plans(user_id,name,mode,default_meals_per_day,goal,activity_level,
            calculation_sex,birth_year,age_years,height_cm,weight_kg,calculation_policy_version,
            rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,
            calculated_at,created_at,updated_at)
        SELECT user_id,@Name,mode,default_meals_per_day,goal,activity_level,
            calculation_sex,birth_year,age_years,height_cm,weight_kg,calculation_policy_version,
            rmr_kcal,maintenance_kcal,target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g,
            calculated_at,@TimestampUtc,@TimestampUtc FROM nutrition_plans WHERE user_id=@CurrentUserId AND id=@PlanId;
        SELECT LAST_INSERT_ID();
        """;
    internal const string ClearCurrent = "UPDATE meal_plan_user_settings SET current_nutrition_plan_id=NULL WHERE user_id=@CurrentUserId AND current_nutrition_plan_id=@PlanId;";
    internal const string Delete = "DELETE FROM nutrition_plans WHERE user_id=@CurrentUserId AND id=@PlanId;";
    internal const string SetCurrent = "UPDATE meal_plan_user_settings SET current_nutrition_plan_id=@PlanId WHERE user_id=@CurrentUserId;";
}
