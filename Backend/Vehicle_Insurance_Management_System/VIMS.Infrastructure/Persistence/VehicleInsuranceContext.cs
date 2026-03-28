using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VIMS.Domain.Entities;

namespace VIMS.Infrastructure.Persistence
{
    public class VehicleInsuranceContext:DbContext
    {
        public VehicleInsuranceContext(DbContextOptions options) : base(options)
        {
        }
        public DbSet<User> Users => Set<User>();
        public DbSet<Vehicle> Vehicles => Set<Vehicle>();
        public DbSet<PolicyPlan> PolicyPlans => Set<PolicyPlan>();
        public DbSet<Policy> Policies => Set<Policy>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Claims> Claims => Set<Claims>();
        public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();
        public DbSet<VehicleApplication> VehicleApplications => Set<VehicleApplication>();
        public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();
        public DbSet<PolicyTransfer> PolicyTransfers => Set<PolicyTransfer>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Garage> Garages => Set<Garage>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Referral> Referrals => Set<Referral>();
        public DbSet<Wallet> Wallets => Set<Wallet>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ================= NOTIFICATION =================
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= UNIQUE CONSTRAINTS =================
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ReferralCode)
                .IsUnique()
                .HasFilter("[ReferralCode] IS NOT NULL");

            modelBuilder.Entity<Vehicle>()
                .HasIndex(v => v.RegistrationNumber)
                .IsUnique();

            modelBuilder.Entity<Policy>()
                .HasIndex(p => p.PolicyNumber)
                .IsUnique();

            modelBuilder.Entity<Claims>()
                .HasIndex(c => c.ClaimNumber)
                .IsUnique();

            // ================= VEHICLE =================
            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.Customer)
                .WithMany(u => u.Vehicles)
                .HasForeignKey(v => v.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= POLICY =================
            modelBuilder.Entity<Policy>()
                .HasOne(p => p.Customer)
                .WithMany(u => u.CustomerPolicies)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Policy>()
                .HasOne(p => p.Agent)
                .WithMany(u => u.AgentPolicies)
                .HasForeignKey(p => p.AgentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Policy>()
                .HasOne(p => p.Vehicle)
                .WithMany(v => v.Policies)
                .HasForeignKey(p => p.VehicleId);

            modelBuilder.Entity<Policy>()
                .HasOne(p => p.Plan)
                .WithMany(pl => pl.Policies)
                .HasForeignKey(p => p.PlanId);

            // ================= PAYMENT =================
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Policy)
                .WithMany(p => p.Payments)
                .HasForeignKey(p => p.PolicyId);

            // ================= REFERRAL/WALLET =================
            modelBuilder.Entity<User>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(u => u.ReferredByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Referral>()
                .HasOne(r => r.ReferrerUser)
                .WithMany(u => u.ReferralsMade)
                .HasForeignKey(r => r.ReferrerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Referral>()
                .HasOne(r => r.RefereeUser)
                .WithMany(u => u.ReferralsReceived)
                .HasForeignKey(r => r.RefereeUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Referral>()
                .HasIndex(r => r.RefereeUserId)
                .IsUnique();

            modelBuilder.Entity<Wallet>()
                .HasOne(w => w.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<Wallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Wallet>()
                .HasIndex(w => w.UserId)
                .IsUnique();

            // ================= CLAIM =================
            modelBuilder.Entity<Claims>()
                .HasOne(c => c.Policy)
                .WithMany(p => p.Claims)
                .HasForeignKey(c => c.PolicyId);

            modelBuilder.Entity<Claims>()
                .HasOne(c => c.Customer)
                .WithMany(u => u.CustomerClaims)
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Claims>()
                .HasOne(c => c.ClaimsOfficer)
                .WithMany(u => u.ClaimsHandled)
                .HasForeignKey(c => c.ClaimsOfficerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= CLAIM DOCUMENT =================
            modelBuilder.Entity<ClaimDocument>()
                .HasOne(d => d.Claim)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.ClaimId);
            // ================= DECIMAL PRECISION =================

            modelBuilder.Entity<Policy>()
                .Property(p => p.PremiumAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Referral>()
                .Property(r => r.DiscountAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Referral>()
                .Property(r => r.RewardAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Wallet>()
                .Property(w => w.Balance)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Claims>()
                .Property(c => c.ApprovedAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PolicyPlan>()
                .Property(p => p.BasePremium)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PolicyPlan>()
                .Property(p => p.MaxCoverageAmount)
                .HasPrecision(18, 2);


            // ================= COMMISSION =================

            // ================= DEDUCTIBLE =================
            modelBuilder.Entity<PolicyPlan>()
                .Property(p => p.DeductibleAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<VehicleApplication>()
                .HasMany(a => a.Documents)
                .WithOne(d => d.VehicleApplication)
                .HasForeignKey(d => d.VehicleApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Vehicle>()
                .HasOne(v => v.VehicleApplication)
                .WithOne()
                .HasForeignKey<Vehicle>(v => v.VehicleApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<VehicleApplication>()
    .HasOne(v => v.Customer)
    .WithMany()
    .HasForeignKey(v => v.CustomerId)
    .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleApplication>()
                .HasOne(v => v.AssignedAgent)
                .WithMany()
                .HasForeignKey(v => v.AssignedAgentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleApplication>()
                .HasOne(v => v.Plan)
                .WithMany()
                .HasForeignKey(v => v.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Policy>()
        .Property(p => p.IDV)
        .HasPrecision(18, 2);

            modelBuilder.Entity<Policy>()
                .Property(p => p.InvoiceAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<VehicleApplication>()
                .Property(v => v.InvoiceAmount)
                .HasPrecision(18, 2);

            // ================= POLICY TRANSFER =================
            modelBuilder.Entity<PolicyTransfer>()
                .HasOne(t => t.SenderCustomer)
                .WithMany()
                .HasForeignKey(t => t.SenderCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PolicyTransfer>()
                .HasOne(t => t.RecipientCustomer)
                .WithMany()
                .HasForeignKey(t => t.RecipientCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PolicyTransfer>()
                .HasOne(t => t.Policy)
                .WithMany()
                .HasForeignKey(t => t.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PolicyTransfer>()
                .HasOne(t => t.NewVehicleApplication)
                .WithMany()
                .HasForeignKey(t => t.NewVehicleApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
