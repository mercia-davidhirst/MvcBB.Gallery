using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MvcBB.API.SQL;
using MvcBB.ModKit;

namespace MvcBB.Gallery.Api
{
    public class GalleryMod : IApiMod
    {
        public string Id => "gallery";
        public string Name => "Gallery";
        public string Version => "1.1.1";
        public string MinCoreVersion => "1.0.0";
        public string? MaxCoreVersion => null;

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            var dataProvider = configuration["DataProvider"] ?? "InMemory";
            switch (dataProvider)
            {
                case "Sql":
                    services.AddScoped<IGalleryCategoryRepository, SqlGalleryCategoryRepository>();
                    services.AddScoped<IGalleryImageRepository, SqlGalleryImageRepository>();
                    break;
                case "PostgreSql":
                    services.AddScoped<IGalleryCategoryRepository, PostgreSqlGalleryCategoryRepository>();
                    services.AddScoped<IGalleryImageRepository, PostgreSqlGalleryImageRepository>();
                    break;
                default:
                    services.AddSingleton<GalleryInMemoryStore>();
                    services.AddSingleton<IGalleryCategoryRepository, InMemoryGalleryCategoryRepository>();
                    services.AddSingleton<IGalleryImageRepository, InMemoryGalleryImageRepository>();
                    break;
            }
        }

        public async Task InstallAsync(IModInstallContext context)
        {
            switch (context.DataProvider)
            {
                case "Sql":
                    await InstallSqlServerAsync(context.GetConnection()
                        ?? throw new InvalidOperationException("Expected a SQL Server connection."));
                    break;
                case "PostgreSql":
                    await InstallPostgreSqlAsync(context.GetConnection()
                        ?? throw new InvalidOperationException("Expected a PostgreSQL connection."));
                    break;
                default:
                    // InMemory has no database to install into - InMemoryGalleryCategoryRepository/
                    // InMemoryGalleryImageRepository seed themselves instead.
                    break;
            }
        }

        private static async Task InstallSqlServerAsync(System.Data.IDbConnection connection)
        {
            await connection.ExecuteAsync(@"
                IF OBJECT_ID('dbo.Mod_Gallery_Categories') IS NULL
                BEGIN
                    CREATE TABLE dbo.Mod_Gallery_Categories
                    (
                        Id          INT IDENTITY(1,1) NOT NULL,
                        Name        NVARCHAR(200)      NOT NULL,
                        Description NVARCHAR(1000)     NULL,
                        SortOrder   INT                NOT NULL CONSTRAINT DF_Mod_Gallery_Categories_SortOrder DEFAULT (0),
                        CreatedAt   DATETIME2(3)       NOT NULL CONSTRAINT DF_Mod_Gallery_Categories_CreatedAt DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT PK_Mod_Gallery_Categories PRIMARY KEY CLUSTERED (Id)
                    );
                END");

            await connection.ExecuteAsync(@"
                IF OBJECT_ID('dbo.Mod_Gallery_Images') IS NULL
                BEGIN
                    CREATE TABLE dbo.Mod_Gallery_Images
                    (
                        Id               INT IDENTITY(1,1) NOT NULL,
                        CategoryId       INT                NOT NULL,
                        Name             NVARCHAR(200)      NOT NULL,
                        Description      NVARCHAR(2000)     NULL,
                        ImageUrl         NVARCHAR(500)      NOT NULL,
                        FileSizeBytes    BIGINT             NOT NULL CONSTRAINT DF_Mod_Gallery_Images_FileSizeBytes DEFAULT (0),
                        UploadedByUserId INT                NOT NULL,
                        UploadedAt       DATETIME2(3)       NOT NULL CONSTRAINT DF_Mod_Gallery_Images_UploadedAt DEFAULT (SYSUTCDATETIME()),
                        ViewCount        INT                NOT NULL CONSTRAINT DF_Mod_Gallery_Images_ViewCount DEFAULT (0),
                        LikeCount        INT                NOT NULL CONSTRAINT DF_Mod_Gallery_Images_LikeCount DEFAULT (0),
                        CommentCount     INT                NOT NULL CONSTRAINT DF_Mod_Gallery_Images_CommentCount DEFAULT (0),
                        CONSTRAINT PK_Mod_Gallery_Images PRIMARY KEY CLUSTERED (Id),
                        CONSTRAINT FK_Mod_Gallery_Images_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Mod_Gallery_Categories (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_Mod_Gallery_Images_UploadedByUser FOREIGN KEY (UploadedByUserId) REFERENCES dbo.Users (Id)
                    );

                    CREATE NONCLUSTERED INDEX IX_Mod_Gallery_Images_CategoryId_UploadedAt ON dbo.Mod_Gallery_Images (CategoryId, UploadedAt DESC);
                END");

            // 1.1.0: one entry per stored file, so no entry can point at (and
            // later get deleted along with) another member's file. Skipped if
            // existing data already has duplicates; the API's own check still
            // applies to new uploads either way.
            await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Mod_Gallery_Images_ImageUrl' AND object_id = OBJECT_ID('dbo.Mod_Gallery_Images'))
                   AND NOT EXISTS (SELECT ImageUrl FROM dbo.Mod_Gallery_Images GROUP BY ImageUrl HAVING COUNT(*) > 1)
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX UX_Mod_Gallery_Images_ImageUrl ON dbo.Mod_Gallery_Images (ImageUrl);
                END");

            await connection.ExecuteAsync(@"
                IF OBJECT_ID('dbo.Mod_Gallery_Comments') IS NULL
                BEGIN
                    CREATE TABLE dbo.Mod_Gallery_Comments
                    (
                        Id              INT IDENTITY(1,1) NOT NULL,
                        ImageId         INT                NOT NULL,
                        Content         NVARCHAR(2000)     NOT NULL,
                        CreatedByUserId INT                NOT NULL,
                        CreatedAt       DATETIME2(3)       NOT NULL CONSTRAINT DF_Mod_Gallery_Comments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT PK_Mod_Gallery_Comments PRIMARY KEY CLUSTERED (Id),
                        CONSTRAINT FK_Mod_Gallery_Comments_Image FOREIGN KEY (ImageId) REFERENCES dbo.Mod_Gallery_Images (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_Mod_Gallery_Comments_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id)
                    );

                    CREATE NONCLUSTERED INDEX IX_Mod_Gallery_Comments_ImageId ON dbo.Mod_Gallery_Comments (ImageId);
                END");

            await connection.ExecuteAsync(@"
                IF OBJECT_ID('dbo.Mod_Gallery_Likes') IS NULL
                BEGIN
                    CREATE TABLE dbo.Mod_Gallery_Likes
                    (
                        Id        INT IDENTITY(1,1) NOT NULL,
                        ImageId   INT                NOT NULL,
                        UserId    INT                NOT NULL,
                        CreatedAt DATETIME2(3)       NOT NULL CONSTRAINT DF_Mod_Gallery_Likes_CreatedAt DEFAULT (SYSUTCDATETIME()),
                        CONSTRAINT PK_Mod_Gallery_Likes PRIMARY KEY CLUSTERED (Id),
                        CONSTRAINT UQ_Mod_Gallery_Likes_Image_User UNIQUE (ImageId, UserId),
                        CONSTRAINT FK_Mod_Gallery_Likes_Image FOREIGN KEY (ImageId) REFERENCES dbo.Mod_Gallery_Images (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_Mod_Gallery_Likes_User FOREIGN KEY (UserId) REFERENCES dbo.Users (Id)
                    );
                END");
        }

        private static async Task InstallPostgreSqlAsync(System.Data.IDbConnection connection)
        {
            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS public.mod_gallery_categories
                (
                    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    name        VARCHAR(200)    NOT NULL,
                    description VARCHAR(1000)   NULL,
                    sort_order  INTEGER         NOT NULL DEFAULT 0,
                    created_at  TIMESTAMP(3)    NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
                );");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS public.mod_gallery_images
                (
                    id                  INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    category_id         INTEGER         NOT NULL REFERENCES public.mod_gallery_categories (id) ON DELETE CASCADE,
                    name                VARCHAR(200)    NOT NULL,
                    description         VARCHAR(2000)   NULL,
                    image_url           VARCHAR(500)    NOT NULL,
                    file_size_bytes     BIGINT          NOT NULL DEFAULT 0,
                    uploaded_by_user_id INTEGER         NOT NULL REFERENCES public.users (id),
                    uploaded_at         TIMESTAMP(3)    NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
                    view_count          INTEGER         NOT NULL DEFAULT 0,
                    like_count          INTEGER         NOT NULL DEFAULT 0,
                    comment_count       INTEGER         NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS ix_mod_gallery_images_category_id_uploaded_at ON public.mod_gallery_images (category_id, uploaded_at DESC);");

            // 1.1.0: one entry per stored file (see the SQL Server version above).
            await connection.ExecuteAsync(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM public.mod_gallery_images GROUP BY image_url HAVING COUNT(*) > 1) THEN
                        CREATE UNIQUE INDEX IF NOT EXISTS ux_mod_gallery_images_image_url ON public.mod_gallery_images (image_url);
                    END IF;
                END $$;");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS public.mod_gallery_comments
                (
                    id                 INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    image_id           INTEGER         NOT NULL REFERENCES public.mod_gallery_images (id) ON DELETE CASCADE,
                    content             VARCHAR(2000)  NOT NULL,
                    created_by_user_id INTEGER         NOT NULL REFERENCES public.users (id),
                    created_at         TIMESTAMP(3)    NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
                );

                CREATE INDEX IF NOT EXISTS ix_mod_gallery_comments_image_id ON public.mod_gallery_comments (image_id);");

            await connection.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS public.mod_gallery_likes
                (
                    id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                    image_id   INTEGER         NOT NULL REFERENCES public.mod_gallery_images (id) ON DELETE CASCADE,
                    user_id    INTEGER         NOT NULL REFERENCES public.users (id),
                    created_at TIMESTAMP(3)    NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
                    UNIQUE (image_id, user_id)
                );");
        }
    }
}
