USE VehicleInsuranceDB;
GO

UPDATE PolicyPlans
SET MaxCoverageAmount = 
	CASE 
		WHEN LOWER(PlanName) LIKE '%zero%' THEN 1000000
		WHEN LOWER(PlanName) LIKE '%comprehensive%' THEN 500000
		ELSE 100000 
	END
WHERE MaxCoverageAmount <= 0;
GO
