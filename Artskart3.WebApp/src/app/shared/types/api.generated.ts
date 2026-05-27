export interface paths {
    "/api/Search/Observation": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["ObservationSearchFilterDto"];
                    "text/json": components["schemas"]["ObservationSearchFilterDto"];
                    "application/*+json": components["schemas"]["ObservationSearchFilterDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["PagedObservationResponseDto"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        ObservationSearchFilterDto: {
            /** Format: int32 */
            pageNumber?: number | null;
            /** Format: int32 */
            resultsPerPage?: number | null;
            readonly isPaginated?: boolean;
            preferredPopularName?: string | null;
            scientificName?: string | null;
            author?: string | null;
            taxonGroupIds?: number[] | null;
            risikokategoriIder?: number[] | null;
            organizationIds?: number[] | null;
            locality?: string | null;
            municipalityIds?: string[] | null;
        };
        PagedObservationResponseDto: {
            items?: components["schemas"]["ObservationDto"][] | null;
            /** Format: int32 */
            pageNumber?: number;
            /** Format: int32 */
            resultsPerPage?: number;
            /** Format: int32 */
            lookaheadCount?: number;
        };
        ObservationDto: {
            /** Format: int32 */
            id?: number;
            preferredPopularName?: string | null;
            scientificName?: string | null;
            author?: string | null;
            institution?: string | null;
            locality?: string | null;
            municipalityId?: string | null;
            /** Format: int32 */
            taxonGroupId?: number | null;
            /** Format: int32 */
            categoryId?: number | null;
            /** Format: date-time */
            dateTimeCollected?: string | null;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
