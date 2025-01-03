﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmallSafe.Web.Data;

#nullable disable

namespace SmallSafe.Web.Migrations
{
    [DbContext(typeof(SqliteDataContext))]
    partial class SqliteDataContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("SmallSafe.Web.Data.Models.UserAccount", b =>
                {
                    b.Property<int>("UserAccountId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedDateTime")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DeletedDateTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("DropboxAccessToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("DropboxRefreshToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastTwoFactorFailure")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastTwoFactorSuccess")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastUpdateDateTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("SafeDb")
                        .HasColumnType("TEXT");

                    b.Property<int>("TwoFactorFailureCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TwoFactorKey")
                        .HasColumnType("TEXT");

                    b.HasKey("UserAccountId");

                    b.ToTable("UserAccounts");
                });

            modelBuilder.Entity("SmallSafe.Web.Data.Models.UserAccountCredential", b =>
                {
                    b.Property<int>("UserAccountCredentialId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedDateTime")
                        .HasColumnType("TEXT");

                    b.Property<byte[]>("CredentialId")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.Property<DateTime?>("DeletedDateTime")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("LastUpdateDateTime")
                        .HasColumnType("TEXT");

                    b.Property<byte[]>("PublicKey")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.Property<uint>("SignatureCount")
                        .HasColumnType("INTEGER");

                    b.Property<int>("UserAccountId")
                        .HasColumnType("INTEGER");

                    b.Property<byte[]>("UserHandle")
                        .IsRequired()
                        .HasColumnType("BLOB");

                    b.HasKey("UserAccountCredentialId");

                    b.HasIndex("UserAccountId");

                    b.ToTable("UserAccountCredentials");
                });

            modelBuilder.Entity("SmallSafe.Web.Data.Models.UserAccountCredential", b =>
                {
                    b.HasOne("SmallSafe.Web.Data.Models.UserAccount", "UserAccount")
                        .WithMany()
                        .HasForeignKey("UserAccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("UserAccount");
                });
#pragma warning restore 612, 618
        }
    }
}
