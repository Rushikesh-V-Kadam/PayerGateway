using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.OpenApi;
using PayerGateway.Api.Background;
using PayerGateway.Api.Clients;
using PayerGateway.Api.Configuration;
using PayerGateway.Api.Constants;
using PayerGateway.Api.Filters;
using PayerGateway.Api.Helpers;
using PayerGateway.Api.Middleware;
using PayerGateway.Api.Repositories;
using PayerGateway.Api.Services.Cdex;
using PayerGateway.Api.Services.Crd;
using PayerGateway.Api.Services.Dtr;
using PayerGateway.Api.Services.Pas;
using PayerGateway.Api.Services.Payers;
using PayerGateway.Api.Services.Security;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace PayerGateway.Api;

/// <summary>
/// Wires the application together. Every class has its own file and comments; this class only registers them.
///
///   STEP 0  Payers   GET  /api/v1/payers, POST /api/v1/payers/{payerName}/discover
///   STEP 1  CRD      POST /api/v1/crd/order-sign, GET /api/v1/crd/decisions/{orderId}
///   STEP 2  DTR      POST /api/v1/dtr/forms, GET /api/v1/dtr/forms/{formId}, POST /api/v1/dtr/forms/{formId}/answers
///                    (later, payer SMART apps: GET /api/v1/dtr/launch/{linkId} + relay endpoints)
///   STEP 3  PAS      POST /api/v1/pas/requests, PUT / cancel / GET / inquire, POST /api/v1/pas/notifications/{payerName}
///   STEP 4  CDex     GET  /api/v1/cdex/requests/{requestId}/information-requests, POST .../attachments,
///                    POST /api/v1/cdex/tasks/{payerName}
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;

    /// <summary>Receives appsettings.json (and environment overrides).</summary>
    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>Registers settings, clients, storage, workflow services, MVC, error handling and Swagger.</summary>
    public void ConfigureServices(IServiceCollection services)
    {
        // ------------------------------------------------------------ 1. settings (appsettings.json sections)
        services.Configure<FhirApiSettings>(_configuration.GetSection(FhirApiSettings.SectionName));
        services.Configure<EhrSettings>(_configuration.GetSection(EhrSettings.SectionName));
        services.Configure<SecuritySettings>(_configuration.GetSection(SecuritySettings.SectionName));
        services.Configure<WorkflowSettings>(_configuration.GetSection(WorkflowSettings.SectionName));
        services.Configure<List<PayerSettings>>(_configuration.GetSection(PayerSettings.SectionName));

        StorageSettings storage = new StorageSettings();
        _configuration.GetSection(StorageSettings.SectionName).Bind(storage);

        // ------------------------------------------------------------ 2. HTTP (one factory; each client sets its own timeout per call)
        services.AddHttpClient();

        // ------------------------------------------------------------ 3. clients, payers, security (one instance for the app)
        services.AddSingleton<FhirApiClient>();
        services.AddSingleton<EhrClient>();
        services.AddSingleton<PayerRegistry>();
        services.AddSingleton<JwtSigner>();
        services.AddSingleton<PayerClient>();

        // ------------------------------------------------------------ 4. storage: Oracle or in memory
        if (storage.IsOracle())
        {
            services.AddSingleton<OracleDb>();
            services.AddSingleton<ICrdRepository, OracleCrdRepository>();
            services.AddSingleton<IDtrLinkRepository, OracleDtrLinkRepository>();
            services.AddSingleton<IDtrFormRepository, OracleDtrFormRepository>();
            services.AddSingleton<IPriorAuthRepository, OraclePriorAuthRepository>();
            services.AddSingleton<ICdexTaskRepository, OracleCdexTaskRepository>();
            services.AddSingleton<IPayerSubscriptionRepository, OraclePayerSubscriptionRepository>();
        }
        else
        {
            services.AddSingleton<ICrdRepository, InMemoryCrdRepository>();
            services.AddSingleton<IDtrLinkRepository, InMemoryDtrLinkRepository>();
            services.AddSingleton<IDtrFormRepository, InMemoryDtrFormRepository>();
            services.AddSingleton<IPriorAuthRepository, InMemoryPriorAuthRepository>();
            services.AddSingleton<ICdexTaskRepository, InMemoryCdexTaskRepository>();
            services.AddSingleton<IPayerSubscriptionRepository, InMemoryPayerSubscriptionRepository>();
        }

        // ------------------------------------------------------------ 5. workflow services (one instance per request)
        services.AddScoped<CdsHookBuilder>();
        services.AddScoped<CrdService>();
        services.AddScoped<DtrService>();
        services.AddScoped<PasBundleBuilder>();
        services.AddScoped<PasSubscriptionService>();
        services.AddScoped<PasService>();
        services.AddScoped<CdexService>();
        services.AddScoped<EhrApiKeyFilter>();

        // Safety net: retries EHR write-backs, reads payer Tasks, inquires pended requests
        services.AddHostedService<PendingRequestPoller>();

        // ------------------------------------------------------------ 6. MVC controllers, validation errors, exceptions
        services.AddControllers(ConfigureMvc);
        services.Configure<ApiBehaviorOptions>(ConfigureApiBehavior);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();

        // The SMART DTR app (future) runs in a browser and calls the relay endpoints. Restrict origins in production.
        services.AddCors(ConfigureCors);

        // ------------------------------------------------------------ 7. Swagger
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);
    }

    /// <summary>The request pipeline: errors first, then Swagger, CORS and the controllers.</summary>
    public void Configure(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSwagger();
        app.UseSwaggerUI(ConfigureSwaggerUi);
        app.UseCors();
        app.MapControllers();
    }

    // ================================================================== option methods (named methods instead of lambdas)

    /// <summary>Accepts "application/fhir+json" request bodies (payer notifications, pushed Tasks, DTR app relay).</summary>
    private static void ConfigureMvc(MvcOptions options)
    {
        // Only [Required] fields are required (services check the rest and return clear messages)
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

        foreach (IInputFormatter formatter in options.InputFormatters)
        {
            SystemTextJsonInputFormatter? json = formatter as SystemTextJsonInputFormatter;
            if (json != null)
            {
                json.SupportedMediaTypes.Add(ApiHeaders.FhirJson);
            }
        }
    }

    /// <summary>Invalid request bodies (wrong JSON, missing required fields) return our ApiError format with 400.</summary>
    private static void ConfigureApiBehavior(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = ModelValidationResponse.Create;
    }

    /// <summary>Allows any origin (the DTR app is a browser app). Restrict in production.</summary>
    private static void ConfigureCors(CorsOptions options)
    {
        CorsPolicyBuilder policy = new CorsPolicyBuilder();
        policy.AllowAnyOrigin();
        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
        options.AddDefaultPolicy(policy.Build());
    }

    /// <summary>Swagger document: title, XML comments (descriptions + examples) and the EHR API key header.</summary>
    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        OpenApiInfo info = new OpenApiInfo();
        info.Title = "Payer Gateway API";
        info.Version = "v1";
        info.Description =
            "Da Vinci prior authorization for the EHR. Call order: " +
            "1) POST /api/v1/crd/order-sign  2) DTR forms (when documentation is needed)  " +
            "3) POST /api/v1/pas/requests (when prior auth is needed)  4) CDex attachments (when the payer pends and asks for more). " +
            "Every EHR request carries payerName; the gateway finds that payer in appsettings \"Payers\". " +
            "Errors always return { \"error\": \"...\", \"details\": [ ... ], \"traceId\": \"...\" }.";
        options.SwaggerDoc("v1", info);

        string xmlFile = Path.Combine(AppContext.BaseDirectory, "PayerGateway.Api.xml");
        if (File.Exists(xmlFile))
        {
            options.IncludeXmlComments(xmlFile, true);
        }

        // non-nullable properties are shown as required
        options.SupportNonNullableReferenceTypes();

        OpenApiSecurityScheme apiKey = new OpenApiSecurityScheme();
        apiKey.Name = ApiHeaders.EhrApiKey;
        apiKey.In = ParameterLocation.Header;
        apiKey.Type = SecuritySchemeType.ApiKey;
        apiKey.Description = "EHR API key (Security:EhrApiKey in appsettings). Leave empty when no key is configured.";
        options.AddSecurityDefinition("EhrApiKey", apiKey);

        // Swashbuckle 10 (Microsoft.OpenApi 2.x): the requirement is built per document by a method
        options.AddSecurityRequirement(ApiKeyRequirement);
    }

    /// <summary>Every operation may send the EhrApiKey header (Swashbuckle 10 / Microsoft.OpenApi 2.x style).</summary>
    private static OpenApiSecurityRequirement ApiKeyRequirement(OpenApiDocument document)
    {
        OpenApiSecurityRequirement requirement = new OpenApiSecurityRequirement();
        requirement.Add(new OpenApiSecuritySchemeReference("EhrApiKey", document), new List<string>());
        return requirement;
    }

    /// <summary>Swagger UI options.</summary>
    private static void ConfigureSwaggerUi(SwaggerUIOptions options)
    {
        options.DocumentTitle = "Payer Gateway API";
        options.DisplayRequestDuration();
    }
}
