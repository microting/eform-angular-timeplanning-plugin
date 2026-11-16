-- Post-migration patch for shard t (#5489, the web planner "Planlægning (beta)").
--
-- Shard t uses the shard-a seed. The workflow runs this AFTER the plugin is
-- enabled and migrated (b/activate-plugin.spec.ts) and BEFORE the shard's
-- specs, against the plugin database; the SDK tables are named in full.
--
-- What the planner needs that the seed lacks: the signed-in admin must be a
-- MANAGER of the workers, because registering absence (the absence dialog and
-- the editor's absence button) is a manager's decision. A manager is the SDK
-- worker whose e-mail is the signed-in user's, whose AssignedSite has
-- IsManager set, and whose managing tags are on the workers' sites. Nothing in
-- the API links a user's e-mail to a worker, so it is done here.
--
-- Worker "aa ab" (site 16200) becomes admin@admin.com's worker and manages
-- every other active worker through the tag "Planner t". Admin keeps seeing
-- every worker either way: an admin's scope is the whole organisation.

SET @managerSiteUid := 16200;

UPDATE `420_SDK`.Workers w
  JOIN `420_SDK`.SiteWorkers sw ON sw.WorkerId = w.Id
  JOIN `420_SDK`.Sites s ON s.Id = sw.SiteId
SET w.Email = 'admin@admin.com'
WHERE s.MicrotingUid = @managerSiteUid;

INSERT INTO `420_SDK`.Tags (CreatedAt, UpdatedAt, Name, TaggingsCount, Version, WorkflowState)
VALUES (NOW(), NOW(), 'Planner t', 0, 1, 'created');
SET @tagId := LAST_INSERT_ID();

INSERT INTO `420_SDK`.SiteTags (Version, WorkflowState, CreatedAt, UpdatedAt, TagId, SiteId)
SELECT 1, 'created', NOW(), NOW(), @tagId, s.Id
FROM `420_SDK`.Sites s
WHERE s.MicrotingUid IN (SELECT SiteId FROM AssignedSites WHERE WorkflowState = 'created')
  AND s.MicrotingUid <> @managerSiteUid;

UPDATE AssignedSites
SET IsManager = 1
WHERE SiteId = @managerSiteUid AND WorkflowState = 'created';

INSERT INTO AssignedSiteManagingTags
  (AssignedSiteId, TagId, CreatedAt, UpdatedAt, WorkflowState, CreatedByUserId, UpdatedByUserId, Version)
SELECT Id, @tagId, NOW(6), NOW(6), 'created', 1, 1, 1
FROM AssignedSites
WHERE SiteId = @managerSiteUid AND WorkflowState = 'created';
