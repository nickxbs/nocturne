/**
 * Remote functions for treatments report page.
 * Data comes from V4 decomposed endpoints (boluses, carb intakes, BG checks, notes, device events).
 */
import { z } from 'zod';
import { getRequestEvent, form, command, query } from '$app/server';
import { invalid } from '@sveltejs/kit';
export { createEntry, updateEntry } from '$api/entries.remote';
import { DateRangeSchema, resolveReportRange } from '$api/report-range';

async function fetchEntries(startDate: string, endDate: string) {
	const { apiClient } = getRequestEvent().locals;
	const [
		bolusResponse,
		carbResponse,
		bgCheckResponse,
		noteResponse,
		deviceEventResponse,
		basalInjectionResponse,
	] = await Promise.all([
		apiClient.bolus.getAll(startDate, endDate, 10000),
		apiClient.nutrition.getCarbIntakes(startDate, endDate, 10000),
		apiClient.bGCheck.getAll(startDate, endDate, 10000),
		apiClient.note.getAll(startDate, endDate, 10000),
		apiClient.deviceEvent.getAll(startDate, endDate, 10000),
		apiClient.basalInjection.getAll(startDate, endDate, 10000),
	]);

	return {
		boluses: bolusResponse.data ?? [],
		carbIntakes: carbResponse.data ?? [],
		bgChecks: bgCheckResponse.data ?? [],
		notes: noteResponse.data ?? [],
		deviceEvents: deviceEventResponse.data ?? [],
		basalInjections: basalInjectionResponse.data ?? [],
	};
}

/**
 * Get all v4 entry types for the treatments page.
 * Fetches boluses, carb intakes, BG checks, notes, device events and basal injections in parallel.
 */
export const getTreatmentsData = query(
	DateRangeSchema.optional(),
	async (input) => {
		const { startDate, endDate, dayCount } = await resolveReportRange(input);
		const entries = await fetchEntries(startDate, endDate);

		return {
			...entries,
			dateRange: {
				from: startDate,
				to: endDate,
				dayCount,
			},
		};
	}
);

/**
 * Delete a single entry form (v4: dispatches to the correct endpoint by kind)
 */
export const deleteEntryForm = form(
	z.object({
		entryId: z.string().min(1, 'Entry ID is required'),
		entryKind: z.enum(['bolus', 'carbs', 'bgCheck', 'note', 'deviceEvent', 'basalInjection']),
	}),
	async ({ entryId, entryKind }, issue) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		try {
			switch (entryKind) {
				case 'bolus':
					await apiClient.bolus.delete(entryId);
					break;
				case 'carbs':
					await apiClient.nutrition.deleteCarbIntake(entryId);
					break;
				case 'bgCheck':
					await apiClient.bGCheck.delete(entryId);
					break;
				case 'note':
					await apiClient.note.delete(entryId);
					break;
				case 'deviceEvent':
					await apiClient.deviceEvent.delete(entryId);
					break;
				case 'basalInjection':
					await apiClient.basalInjection.delete(entryId);
					break;
			}

			return {
				success: true,
				message: 'Entry deleted successfully',
				deletedEntryId: entryId,
			};
		} catch (error) {
			console.error('Error deleting entry:', error);
			invalid(issue.entryId('Failed to delete entry. Please try again.'));
		}
	}
);

/**
 * Bulk delete entries command (v4: dispatches each item by kind)
 */
export const bulkDeleteEntries = command(
	z.array(
		z.object({
			id: z.string(),
			kind: z.enum(['bolus', 'carbs', 'bgCheck', 'note', 'deviceEvent', 'basalInjection']),
		})
	),
	async (items) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		const deletedIds: string[] = [];
		const failedIds: string[] = [];

		for (const item of items) {
			try {
				switch (item.kind) {
					case 'bolus':
						await apiClient.bolus.delete(item.id);
						break;
					case 'carbs':
						await apiClient.nutrition.deleteCarbIntake(item.id);
						break;
					case 'bgCheck':
						await apiClient.bGCheck.delete(item.id);
						break;
					case 'note':
						await apiClient.note.delete(item.id);
						break;
					case 'deviceEvent':
						await apiClient.deviceEvent.delete(item.id);
						break;
					case 'basalInjection':
						await apiClient.basalInjection.delete(item.id);
						break;
				}
				deletedIds.push(item.id);
			} catch (err) {
				console.error(`Error deleting ${item.kind} ${item.id}:`, err);
				failedIds.push(item.id);
			}
		}

		if (failedIds.length > 0) {
			return {
				success: false,
				message: `Failed to delete ${failedIds.length} of ${items.length} entries`,
				deletedEntryIds: deletedIds,
				failedEntryIds: failedIds,
			};
		}

		return {
			success: true,
			message: `Successfully deleted ${deletedIds.length} entr${deletedIds.length !== 1 ? 'ies' : 'y'}`,
			deletedEntryIds: deletedIds,
		};
	}
);

