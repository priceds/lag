BINARY := lag
GO ?= go

.PHONY: build test fmt vet check clean

build:
	$(GO) build -trimpath -ldflags "-s -w" -o bin/$(BINARY) .

test:
	$(GO) test ./...

fmt:
	$(GO) fmt ./...

vet:
	$(GO) vet ./...

check: fmt vet test build

clean:
	$(RM) -r bin dist
