import React, { useEffect, useRef, useState } from "react";
import {
  Container,
  TextField,
  Select,
  MenuItem,
  InputLabel,
  FormControl,
  Button,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  CircularProgress,
  Typography,
  Box,
  Stack,
  Snackbar,
  Alert,
} from "@mui/material";
import { JSX } from "react/jsx-runtime";

interface SearchResult {
  docId: string;
  docUrl: string;
  searchScore: number;
  title: string;
}

const PRESET_OPTIONS = [10, 20, 50, 100];
const DEBOUNCE_MS = 500;

export default function App(): JSX.Element {
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [maxResults, setMaxResults] = useState<number>(10);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const debounceRef = useRef<number | undefined>(undefined);

  // Do an immediate search (manual or Enter pressed)
  const doSearchImmediate = async (term = searchTerm, maxR = maxResults) => {
    setLoading(true);
    setError(null);
    try {
      const url = `https://localhost:7124/search?searchTerm=${encodeURIComponent(
        term
      )}&maxResults=${maxR}`;
      const resp = await fetch(url);
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const data: SearchResult[] = await resp.json();
      setResults(data);
    } catch (err: any) {
      console.error("Search error:", err);
      setError(
        err?.message || "Failed to fetch results. Make sure the API is running and CORS allows requests."
      );
      setResults([]);
    } finally {
      setLoading(false);
    }
  };

  // Debounced live search whenever searchTerm or maxResults changes
  useEffect(() => {
    // Clear previous timer
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    // Set new timer
    // Type cast because window.setTimeout returns number in browsers.
    debounceRef.current = window.setTimeout(() => {
      // only trigger if there's some term
      if (searchTerm.trim().length > 0) {
        doSearchImmediate(searchTerm, maxResults);
      }
    }, DEBOUNCE_MS) as unknown as number;

    // cleanup on change/unmount
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchTerm, maxResults]);

  // handle Enter key in the search input
  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      // cancel pending debounce and do immediate search
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
      doSearchImmediate();
    }
  };

  return (
    <Container maxWidth="md" sx={{ mt: 6 }}>
      <Paper elevation={3} sx={{ p: 3 }}>
        <Typography variant="h5" gutterBottom>
          Wikipedia Search
        </Typography>

        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} alignItems="center" sx={{ mb: 2 }}>
          <TextField
            label="Search term"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            onKeyDown={handleKeyDown}
            fullWidth
          />

          <FormControl sx={{ minWidth: 140 }}>
            <InputLabel id="max-results-label">Max results</InputLabel>
            <Select
              labelId="max-results-label"
              label="Max results"
              value={maxResults}
              onChange={(e) => setMaxResults(Number(e.target.value))}
            >
              {PRESET_OPTIONS.map((opt) => (
                <MenuItem key={opt} value={opt}>
                  {opt}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <Button
            variant="contained"
            onClick={() => {
              // cancel pending debounce and do immediate
              if (debounceRef.current) clearTimeout(debounceRef.current);
              doSearchImmediate();
            }}
            sx={{ height: "56px" }}
          >
            Search
          </Button>
        </Stack>

        <Box sx={{ display: "flex", alignItems: "center", gap: 2, mb: 2 }}>
          {loading && <CircularProgress size={20} />}
          <Typography variant="body2" color="text.secondary">
            {loading ? "Searching..." : `${results.length} results`}
          </Typography>
        </Box>

        <Paper sx={{ width: "100%", overflow: "auto" }}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Title</TableCell>
                <TableCell align="right">Score</TableCell>
                <TableCell align="right">DocId</TableCell>
                <TableCell>URL</TableCell>
              </TableRow>
            </TableHead>

            <TableBody>
              {results.map((r) => (
                <TableRow key={r.docId} hover>
                  <TableCell component="th" scope="row">
                    <a
                      href={r.docUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      style={{ textDecoration: "none", color: "#1976d2" }}
                    >
                      {r.title}
                    </a>
                  </TableCell>
                  <TableCell align="right">{r.searchScore.toFixed(2)}</TableCell>
                  <TableCell align="right">{r.docId}</TableCell>
                  <TableCell>
                    <a href={r.docUrl} target="_blank" rel="noopener noreferrer">
                      Open
                    </a>
                  </TableCell>
                </TableRow>
              ))}

              {!loading && results.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} align="center">
                    <Typography variant="body2" color="text.secondary">
                      No results
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Paper>

        <Snackbar
          open={!!error}
          onClose={() => setError(null)}
          autoHideDuration={6000}
          anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
        >
          <Alert severity="error" onClose={() => setError(null)}>
            {error}
          </Alert>
        </Snackbar>
      </Paper>
    </Container>
  );
}
