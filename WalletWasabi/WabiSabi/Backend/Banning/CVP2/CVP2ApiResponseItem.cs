using System.Collections.Generic;

namespace WalletWasabi.WabiSabi.Backend.Banning.CVP2;

#pragma warning disable IDE1006 // Name styles

public record HttpError
(
	string? id,
	string? name,
	string? message
);

public record CVP2ApiResponseItem
(
	string id,
	string type,
	Subject subject,
	Customer customer,
	BlockchainInfo blockchain_info,
	DateTime created_at,
	DateTime updated_at,
	DateTime analysed_at,
	AnalysedBy analysed_by,
	string asset_tier,
	List<ClusterEntity> cluster_entities,
	string team_id,
	double? risk_score,
	RiskScoreDetail risk_score_detail,
	Error? error,
	EvaluationDetail? evaluation_detail,
	Contributions? contributions,
	List<DetectedBehavior> detected_behaviors,
	Changes changes,
	string workflow_status,
	int workflow_status_id,
	string? process_status,
	int? process_status_id,
	List<object> triggered_rules,
	string screening_source
);

public record CVP2ApiResponseItemPartial
(
	string? id,
	double? risk_score,
	string? process_status,
	int? process_status_id
);

public record AnalysedBy
(
	string id,
	string type
);

public record BlockchainInfo
(
	Cluster cluster
);

public record Changes
(
	double risk_score_change
);

public record Cluster
(
	UsdValue inflow_value,
	UsdValue outflow_value
);

public record ClusterEntity
(
	string? name,
	string? category,
	bool is_primary_entity,
	bool? is_vasp
);

public record Contribution
(
	double contribution_percentage,
	double counterparty_percentage,
	double indirect_percentage,
	string entity,
	RiskTriggers risk_triggers,
	CryptoValue contribution_value,
	CryptoValue counterparty_value,
	CryptoValue indirect_value,
	bool is_screened_address,
	int min_number_of_hops
);

public record Contributions
(
	List<Entities>? source,
	List<Entities>? destination
);

public record CryptoValue
(
	double native, // api doesn't use it
	double native_major, // api doesn't use it
	double usd
);

public record Customer
(
	string? id,
	string reference
);

public record Rule
(
	string rule_id,
	string rule_name,
	double risk_score,
	List<MatchedElement> matched_elements,
	List<MatchedBehavior> matched_behaviors,
	string rule_history_id,
	string mc_analysis_id
);

public record Entities
(
	List<Entity>? entities,
	double contribution_percentage,
	CryptoValue contribution_value,
	double counterparty_percentage,
	CryptoValue counterparty_value,
	double indirect_percentage,
	CryptoValue indirect_value,
	bool is_screened_address,
	int min_number_of_hops
);

public record DetectedBehavior
(
	string behavior_type,
	int length,
	double usd_value
);

public record Entity
(
	string? name,
	string category,
	bool is_primary_entity,
	bool? is_vasp
);

public record Error
(
	string message
);

public record EvaluationDetail
(
	List<Rule> source,
	List<Rule> destination
);

public record UsdValue
(
	double? usd
);

public record MatchedBehavior
(
	string behavior_type,
	int length,
	double usd_value
);

public record MatchedElement
(
	string category,
	double contribution_percentage,
	CryptoValue contribution_value,
	double counterparty_percentage,
	CryptoValue counterparty_value,
	double indirect_percentage,
	CryptoValue indirect_value,
	List<Contribution> contributions
);

public record RiskScoreDetail
(
	double? source,
	double? destination
);

public record RiskTriggers
(
	string name,
	string category,
	bool is_sanctioned,
	List<string> country
);

public record Subject
(
	string asset,
	string type,
	string hash
);
