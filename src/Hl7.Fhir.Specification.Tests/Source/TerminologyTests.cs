﻿using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Support;
using Hl7.Fhir.Validation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Hl7.Fhir.Source
{
    public class TerminologyTests : IClassFixture<ValidationFixture>
    {            
        private IResourceResolver _resolver;

        public TerminologyTests(ValidationFixture fixture)
        {
            _resolver = fixture.Resolver;
        }

        [Fact]
        public void ExpansionOfWholeSystem()
        {
            var issueTypeVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/issue-type").DeepCopy() as ValueSet;
            Assert.False(issueTypeVs.HasExpansion);

            // Wipe the version so we don't have to update our tests all the time
            // issueTypeVs.CodeSystem.Version = null;

            var expander = new ValueSetExpander(new ValueSetExpanderSettings { ValueSetSource = _resolver });

            expander.Expand(issueTypeVs);

            Assert.True(issueTypeVs.HasExpansion);
            var id = issueTypeVs.Expansion.Identifier;
            Assert.NotNull(id);

            //TODO: Re-enable test after we fix merging expansiom parameters
            //Assert.False(issueTypeVs.Expansion.Parameter.Any(c => c.Name == "version"));

            Assert.True(issueTypeVs.CodeInExpansion("security", "http://hl7.org/fhir/issue-type"));
            Assert.True(issueTypeVs.CodeInExpansion("expired", "http://hl7.org/fhir/issue-type"));
            Assert.Equal(29, issueTypeVs.Expansion.Contains.CountConcepts());
            Assert.Equal(issueTypeVs.Expansion.Contains.CountConcepts(), issueTypeVs.Expansion.Total);

            var trans = issueTypeVs.FindInExpansion("transient", "http://hl7.org/fhir/issue-type");
            Assert.NotNull(trans);
            Assert.NotNull(trans.FindCode("exception"));

            // Now, make this a versioned system
            issueTypeVs.Version = "3.14";
            expander.Expand(issueTypeVs);
            Assert.NotEqual(id, issueTypeVs.Expansion.Identifier);
            Assert.Equal(29, issueTypeVs.Expansion.Total);

            //var versionParam = issueTypeVs.Expansion.Parameter.Single(c => c.Name == "version");
            //Assert.Equal("http://hl7.org/fhir/ValueSet/issue-type?version=3.14", ((FhirUri)versionParam.Value).Value);
        }


        [Fact]
        public void ExpansionOfComposeInclude()
        {
            var testVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/marital-status").DeepCopy() as ValueSet;
            Assert.False(testVs.HasExpansion);

            var expander = new ValueSetExpander(new ValueSetExpanderSettings { ValueSetSource = _resolver });
            expander.Expand(testVs);
            Assert.Equal(11, testVs.Expansion.Total);
        }


        [Fact]
        public void ExpansionOfComposeImport()
        {
            var testVs = _resolver.ResolveByCanonicalUri("http://hl7.org/fhir/ValueSet/v3-ObservationMethod").DeepCopy() as ValueSet;
            Assert.False(testVs.HasExpansion);

            var expander = new ValueSetExpander(new ValueSetExpanderSettings { ValueSetSource = _resolver });
            expander.Settings.MaxExpansionSize = 50;

            Assert.Throws<ValueSetExpansionTooBigException>( () => expander.Expand(testVs) );

            expander.Settings.MaxExpansionSize = 500;
            expander.Expand(testVs);
            Assert.Equal(304, testVs.Expansion.Total);
        }

        [Fact]
        public void TermServiceLoopupTest()
        {
            var svc = new LocalTerminologyService(_resolver);

            var result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason");
            Assert.True(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaNX", "http://hl7.org/fhir/data-absent-reason");
            Assert.False(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason", display: "Not a Number");
            Assert.True(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/data-absent-reason", "NaN", "http://hl7.org/fhir/data-absent-reason", display: "Not any Number");
            Assert.True(result.Success);
            Assert.Equal(1, result.Warnings);       // Warning for incorrect display

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/v3-AcknowledgementDetailCode", "_AcknowledgementDetailNotSupportedCode",
                "http://hl7.org/fhir/v3/AcknowledgementDetailCode");
            Assert.False(result.Success);

            result = svc.ValidateCode("http://hl7.org/fhir/ValueSet/v3-AcknowledgementDetailCode", "_AcknowledgementDetailNotSupportedCode",
                "http://hl7.org/fhir/v3/AcknowledgementDetailCode", abstractAllowed: true);
            Assert.True(result.Success);

            // But this won't, it's also a composition, but without expansion - the local term server won't help you here
            Assert.Throws<ValueSetExpansionTooComplexException>( 
                () => svc.ValidateCode("http://hl7.org/fhir/ValueSet/allergyintolerance-substance-code", "160244002", "http://snomed.info/sct") );
        }


        [Fact]
        public void TestPropertyRetrieval()
        {
            var testCs = _resolver.FindCodeSystem("http://hl7.org/fhir/item-type");

            var conceptGroup = testCs.Concept.Single(c=>c.Code == "group");
            var conceptQuestion = testCs.Concept.Single(c => c.Code == "question");

            Assert.False(conceptGroup.ListConceptProperties(testCs, CodeSystem.CONCEPTPROPERTY_NOT_SELECTABLE).Any());
            Assert.True(conceptQuestion.ListConceptProperties(testCs, CodeSystem.CONCEPTPROPERTY_NOT_SELECTABLE).Any());
        }


        [Fact, Trait("Category", "IntegrationTest")]
        public void TermExternalServiceValidateCodeTest()
        {
            var client = new FhirClient("http://ontoserver.csiro.au/dstu2_1");
            var service = new ExternalTerminologyService(client);            

            var result = service.ValidateCode("http://hl7.org/fhir/ValueSet/c80-facilitycodes", "4322002", "http://snomed.info/sct");
            Assert.True(result.Success);

            result = service.ValidateCode("http://hl7.org/fhir/ValueSet/c80-facilitycodes", "4322002crap", "http://snomed.info/sct");
            Assert.False(result.Success);

            Assert.Throws<ValueSetUnknownException>(() => service.ValidateCode("http://hl7.org/fhir/ValueSet/c80crap-facilitycodes", "4322002", "http://snomed.info/sct"));
        }

        [Fact, Trait("Category", "IntegrationTest")]
        public void TermFallbackServiceValidateCodeTest()
        {
            var client = new FhirClient("http://ontoserver.csiro.au/dstu2_1");
            var service = new ExternalTerminologyService(client);
            var local = new LocalTerminologyService(_resolver);
            var fallback = new FallbackTerminologyService(local, service);

            // This should still work
            var result = service.ValidateCode("http://hl7.org/fhir/ValueSet/c80-facilitycodes", "4322002", "http://snomed.info/sct");
            Assert.True(result.Success);

            // This should still fail
            result = service.ValidateCode("http://hl7.org/fhir/ValueSet/c80-facilitycodes", "4322002crap", "http://snomed.info/sct");
            Assert.False(result.Success);

            // Now, this should fall back
            result = fallback.ValidateCode("http://hl7.org/fhir/ValueSet/allergyintolerance-substance-code", "160244002", "http://snomed.info/sct");
            Assert.True(result.Success);
        }

        [Fact, Trait("Category", "IntegrationTest")]
        public void TermFallbackServiceValidateCodeTestWithVS()
        {
            var client = new FhirClient("http://ontoserver.csiro.au/dstu2_1");
            var service = new ExternalTerminologyService(client);
            var vs = _resolver.FindValueSet("http://hl7.org/fhir/ValueSet/allergyintolerance-substance-code");
            Assert.NotNull(vs);

            vs.Url = "http://furore.com/fhir/ValueSet/testVS";
            var local = new LocalTerminologyService(new IKnowOnlyMyTestVSResolver(vs));
            var fallback = new FallbackTerminologyService(local, service);

            // Now, this should fall back to external + send our vs (that the server cannot know about)
            var result = fallback.ValidateCode("http://furore.com/fhir/ValueSet/testVS", "160244002", "http://snomed.info/sct");
            Assert.True(result.Success);
        }

        private class IKnowOnlyMyTestVSResolver : IResourceResolver
        {
            public ValueSet _myOnlyVS;

            public IKnowOnlyMyTestVSResolver(ValueSet vs)
            {
                _myOnlyVS = vs;
            }

            public Resource ResolveByCanonicalUri(string uri) => (uri == _myOnlyVS.Url) ? _myOnlyVS : null;

            public Resource ResolveByUri(string uri) => throw new NotImplementedException();
        }

    }
}

